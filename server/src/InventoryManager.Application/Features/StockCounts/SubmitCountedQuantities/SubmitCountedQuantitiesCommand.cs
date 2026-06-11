using FluentValidation;
using InventoryManager.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventoryManager.Application.Features.StockCounts.SubmitCountedQuantities;

// ─── Command ──────────────────────────────────────────────────────────────────
/// <summary>
/// Staff submit the quantities they physically counted for each item.
/// This transitions the count to Submitted status.
///
/// RULES:
/// - Count must be in Created or InProgress status.
/// - All items in the count must have a CountedQuantity provided (even if 0).
/// - Once submitted, counts cannot be changed by staff — only a Manager review
///   can revert to InProgress for a recount.
/// </summary>
public record SubmitCountedQuantitiesCommand(
    Guid StockCountId,
    List<CountedItem> Items
) : IRequest;

public record CountedItem(Guid ProductId, decimal CountedQuantity, string? Notes);

// ─── Validator ────────────────────────────────────────────────────────────────
public class SubmitCountedQuantitiesCommandValidator
    : AbstractValidator<SubmitCountedQuantitiesCommand>
{
    public SubmitCountedQuantitiesCommandValidator()
    {
        RuleFor(x => x.StockCountId).NotEmpty();
        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("At least one counted item must be submitted.")
            .Must(items => items.All(i => i.CountedQuantity >= 0))
            .WithMessage("Counted quantities cannot be negative.");
    }
}

// ─── Handler ──────────────────────────────────────────────────────────────────
public class SubmitCountedQuantitiesCommandHandler
    : IRequestHandler<SubmitCountedQuantitiesCommand>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public SubmitCountedQuantitiesCommandHandler(IAppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task Handle(SubmitCountedQuantitiesCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.Role is UserRole.ReadOnly)
            throw new UnauthorizedAccessException("Read-only users cannot submit stock counts.");

        var stockCount = await _db.StockCounts
            .Include(sc => sc.Items)
            .FirstOrDefaultAsync(sc => sc.Id == request.StockCountId, cancellationToken)
            ?? throw new KeyNotFoundException("Stock count not found.");

        if (stockCount.Status is not StockCountStatus.Created
                               and not StockCountStatus.InProgress)
            throw new InvalidOperationException(
                $"Cannot submit — count is in '{stockCount.Status}' status. " +
                "Only Created or InProgress counts can be submitted.");

        // ── Validate all items are accounted for ──────────────────────────────
        var countItemDict = stockCount.Items.ToDictionary(i => i.ProductId);
        var submittedDict = request.Items.ToDictionary(i => i.ProductId);

        var missingItems = countItemDict.Keys
            .Except(submittedDict.Keys)
            .ToList();

        if (missingItems.Count > 0)
            throw new InvalidOperationException(
                $"All {countItemDict.Count} items must be counted. " +
                $"Missing {missingItems.Count} product(s). Enter 0 if none found.");

        // ── Apply the counted quantities ──────────────────────────────────────
        foreach (var item in stockCount.Items)
        {
            if (!submittedDict.TryGetValue(item.ProductId, out var submitted)) continue;
            item.CountedQuantity = submitted.CountedQuantity;
            item.Notes = submitted.Notes?.Trim();
        }

        stockCount.Status = StockCountStatus.Submitted;
        stockCount.SubmittedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
    }
}
