using FluentValidation;
using InventoryManager.Domain.Entities;
using InventoryManager.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventoryManager.Application.Features.StockCounts.ReviewStockCount;

// ─── Command ──────────────────────────────────────────────────────────────────
/// <summary>
/// Manager reviews the submitted count and posts variances.
///
/// WHAT HAPPENS IN ONE TRANSACTION:
/// 1. For each item with IsApproved = true and a non-zero variance:
///    a. Post a CountAdjustment StockMovement (positive or negative).
///    b. Update the StockLevel at the warehouse to reflect the corrected qty.
/// 2. Items with IsApproved = false are flagged for recount (status stays Reviewing).
///    The manager must approve all items or initiate a new count for the rejects.
/// 3. When ALL items are approved (none rejected), count → Completed.
///
/// DESIGN NOTE: The variance is computed from the entity's computed property.
/// We only post a movement if |variance| > 0 to avoid cluttering the audit trail.
/// </summary>
public record ReviewStockCountCommand(
    Guid StockCountId,
    List<ReviewedItem> ReviewedItems
) : IRequest<ReviewStockCountResult>;

public record ReviewedItem(Guid ProductId, bool IsApproved, string? ManagerNotes);

public record ReviewStockCountResult(
    int ApprovedCount,
    int RejectedCount,
    int AdjustmentsPosted,
    bool IsCompleted
);

// ─── Validator ────────────────────────────────────────────────────────────────
public class ReviewStockCountCommandValidator : AbstractValidator<ReviewStockCountCommand>
{
    public ReviewStockCountCommandValidator()
    {
        RuleFor(x => x.StockCountId).NotEmpty();
        RuleFor(x => x.ReviewedItems).NotEmpty();
    }
}

// ─── Handler ──────────────────────────────────────────────────────────────────
public class ReviewStockCountCommandHandler : IRequestHandler<ReviewStockCountCommand, ReviewStockCountResult>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public ReviewStockCountCommandHandler(IAppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ReviewStockCountResult> Handle(
        ReviewStockCountCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.Role is UserRole.Staff or UserRole.ReadOnly)
            throw new UnauthorizedAccessException("Only Managers and Admins can review stock counts.");

        var stockCount = await _db.StockCounts
            .Include(sc => sc.Items).ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(sc => sc.Id == request.StockCountId, cancellationToken)
            ?? throw new KeyNotFoundException("Stock count not found.");

        if (stockCount.Status is not StockCountStatus.Submitted
                               and not StockCountStatus.Reviewing)
            throw new InvalidOperationException(
                $"Cannot review — count is in '{stockCount.Status}' status. " +
                "Only Submitted or Reviewing counts can be reviewed.");

        stockCount.Status = StockCountStatus.Reviewing;

        var reviewedDict = request.ReviewedItems.ToDictionary(r => r.ProductId);
        var adjustmentsPosted = 0;
        var approvedCount = 0;
        var rejectedCount = 0;

        // Load stock levels for the warehouse — needed to apply adjustments
        var productIds = stockCount.Items.Select(i => i.ProductId).ToList();
        var stockLevels = await _db.StockLevels
            .Where(sl => sl.WarehouseId == stockCount.WarehouseId
                      && productIds.Contains(sl.ProductId))
            .ToDictionaryAsync(sl => sl.ProductId, cancellationToken);

        foreach (var item in stockCount.Items)
        {
            if (!reviewedDict.TryGetValue(item.ProductId, out var review))
                continue;

            item.IsApproved = review.IsApproved;
            item.Notes = review.ManagerNotes is not null
                ? $"{item.Notes}; Manager: {review.ManagerNotes}".TrimStart(';', ' ')
                : item.Notes;

            if (!review.IsApproved)
            {
                rejectedCount++;
                continue;
            }

            approvedCount++;

            // Post a CountAdjustment if there's a variance
            var variance = item.Variance;  // computed property: CountedQty - SystemQty
            if (variance.HasValue && variance.Value != 0)
            {
                if (!stockLevels.TryGetValue(item.ProductId, out var stockLevel))
                {
                    // Edge case: stock record disappeared — create it
                    stockLevel = new StockLevel
                    {
                        Id = Guid.NewGuid(),
                        ProductId = item.ProductId,
                        WarehouseId = stockCount.WarehouseId,
                        QuantityOnHand = 0,
                        QuantityInTransit = 0
                    };
                    _db.StockLevels.Add(stockLevel);
                    stockLevels[item.ProductId] = stockLevel;
                }

                var newQty = stockLevel.QuantityOnHand + variance.Value;
                stockLevel.QuantityOnHand = Math.Max(0, newQty); // Never below zero

                _db.StockMovements.Add(new StockMovement
                {
                    Id = Guid.NewGuid(),
                    ProductId = item.ProductId,
                    WarehouseId = stockCount.WarehouseId,
                    MovementType = MovementType.CountAdjustment,
                    QuantityChange = variance.Value,
                    QuantityAfter = stockLevel.QuantityOnHand,
                    ReferenceType = "StockCount",
                    ReferenceId = stockCount.Id,
                    Reason = variance.Value < 0
                        ? $"Stock count variance: system had {item.SystemQuantity}, " +
                          $"counted {item.CountedQuantity} ({variance.Value} units unexplained loss)."
                        : $"Stock count variance: system had {item.SystemQuantity}, " +
                          $"counted {item.CountedQuantity} ({variance.Value} units found).",
                    CreatedByUserId = _currentUser.UserId
                });

                adjustmentsPosted++;
            }
        }

        // Complete the count only if all items are resolved (no rejections)
        var allItemsResolved = stockCount.Items.All(i => i.IsApproved == true);
        if (allItemsResolved)
        {
            stockCount.Status = StockCountStatus.Completed;
            stockCount.CompletedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);

        return new ReviewStockCountResult(
            approvedCount,
            rejectedCount,
            adjustmentsPosted,
            allItemsResolved
        );
    }
}
