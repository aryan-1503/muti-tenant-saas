using FluentValidation;
using InventoryManager.Domain.Entities;
using InventoryManager.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventoryManager.Application.Features.StockCounts.CreateStockCount;

// ─── Command ──────────────────────────────────────────────────────────────────
/// <summary>
/// Creates a new stock count session for a warehouse.
///
/// WHAT HAPPENS:
/// 1. Snapshot the current SystemQuantity for every in-scope product.
///    Scope = all active products at the warehouse, optionally filtered by CategoryId.
/// 2. Create StockCountItem rows with the snapshot — this is the "expected" count.
/// 3. IsBlindCount = true hides the expected qty from counting staff (recommended),
///    preventing anchoring bias (staff rounding to what they expect to see).
/// 4. Assign to a specific user, or leave unassigned for any staff to fill in.
/// </summary>
public record CreateStockCountCommand(
    Guid WarehouseId,
    Guid? CategoryId,
    Guid? AssignedUserId,
    bool IsBlindCount = true,
    string? Notes = null
) : IRequest<Guid>;

// ─── Validator ────────────────────────────────────────────────────────────────
public class CreateStockCountCommandValidator : AbstractValidator<CreateStockCountCommand>
{
    public CreateStockCountCommandValidator()
    {
        RuleFor(x => x.WarehouseId).NotEmpty();
        RuleFor(x => x.Notes).MaximumLength(1000).When(x => x.Notes is not null);
    }
}

// ─── Handler ──────────────────────────────────────────────────────────────────
public class CreateStockCountCommandHandler : IRequestHandler<CreateStockCountCommand, Guid>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public CreateStockCountCommandHandler(IAppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Guid> Handle(CreateStockCountCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.Role is UserRole.ReadOnly)
            throw new UnauthorizedAccessException("Read-only users cannot create stock counts.");

        // Validate warehouse
        var warehouse = await _db.Warehouses
            .FirstOrDefaultAsync(w => w.Id == request.WarehouseId && w.IsActive, cancellationToken)
            ?? throw new KeyNotFoundException("Warehouse not found or is inactive.");

        // Validate category if provided
        if (request.CategoryId.HasValue)
        {
            var categoryExists = await _db.Categories
                .AnyAsync(c => c.Id == request.CategoryId.Value, cancellationToken);
            if (!categoryExists)
                throw new KeyNotFoundException("Category not found.");
        }

        // Validate assigned user if provided
        if (request.AssignedUserId.HasValue)
        {
            var userExists = await _db.Users
                .AnyAsync(u => u.Id == request.AssignedUserId.Value && u.IsActive, cancellationToken);
            if (!userExists)
                throw new KeyNotFoundException("Assigned user not found or is inactive.");
        }

        // ── Snapshot current stock levels for in-scope products ───────────────
        var stockQuery = _db.StockLevels
            .Where(sl => sl.WarehouseId == request.WarehouseId
                      && sl.Product.IsActive);

        if (request.CategoryId.HasValue)
            stockQuery = stockQuery.Where(sl => sl.Product.CategoryId == request.CategoryId.Value);

        var stockSnapshots = await stockQuery
            .Select(sl => new { sl.ProductId, sl.QuantityOnHand })
            .ToListAsync(cancellationToken);

        if (stockSnapshots.Count == 0)
            throw new InvalidOperationException(
                "No active products with stock records found at this warehouse" +
                (request.CategoryId.HasValue ? " for the selected category." : "."));

        // ── Create the count session ──────────────────────────────────────────
        var count = new StockCount
        {
            Id = Guid.NewGuid(),
            WarehouseId = request.WarehouseId,
            CategoryId = request.CategoryId,
            AssignedUserId = request.AssignedUserId,
            IsBlindCount = request.IsBlindCount,
            Status = StockCountStatus.Created,
            Notes = request.Notes?.Trim(),
            CreatedByUserId = _currentUser.UserId
        };
        _db.StockCounts.Add(count);

        // ── Create one item per product ───────────────────────────────────────
        foreach (var snapshot in stockSnapshots)
        {
            _db.StockCountItems.Add(new StockCountItem
            {
                Id = Guid.NewGuid(),
                StockCountId = count.Id,
                ProductId = snapshot.ProductId,
                SystemQuantity = snapshot.QuantityOnHand,
                CountedQuantity = null,  // Staff fill this in
                IsApproved = null
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
        return count.Id;
    }
}
