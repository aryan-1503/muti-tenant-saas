using FluentValidation;
using InventoryManager.Domain.Entities;
using InventoryManager.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventoryManager.Application.Features.PurchaseOrders.CreatePurchaseOrder;

// ─── Command ──────────────────────────────────────────────────────────────────
/// <summary>
/// Creates a new Purchase Order in Draft status.
/// Draft POs can be edited before being sent to the supplier.
/// Lines contain the product, quantity, and agreed unit cost.
/// </summary>
public record CreatePurchaseOrderCommand(
    Guid SupplierId,
    Guid WarehouseId,
    DateTime? ExpectedDeliveryDate,
    string? Notes,
    List<PurchaseOrderLineInput> Lines
) : IRequest<Guid>;

public record PurchaseOrderLineInput(Guid ProductId, decimal Quantity, decimal UnitCost);

// ─── Validator ────────────────────────────────────────────────────────────────
public class CreatePurchaseOrderCommandValidator : AbstractValidator<CreatePurchaseOrderCommand>
{
    public CreatePurchaseOrderCommandValidator()
    {
        RuleFor(x => x.SupplierId).NotEmpty();
        RuleFor(x => x.WarehouseId).NotEmpty();
        RuleFor(x => x.ExpectedDeliveryDate)
            .GreaterThan(DateTime.UtcNow.Date)
            .When(x => x.ExpectedDeliveryDate.HasValue)
            .WithMessage("Expected delivery date must be in the future.");
        RuleFor(x => x.Lines)
            .NotEmpty().WithMessage("A purchase order must have at least one line.")
            .Must(l => l.All(i => i.Quantity > 0)).WithMessage("All quantities must be positive.")
            .Must(l => l.All(i => i.UnitCost >= 0)).WithMessage("Unit cost cannot be negative.");
        RuleFor(x => x.Notes).MaximumLength(1000).When(x => x.Notes is not null);
    }
}

// ─── Handler ──────────────────────────────────────────────────────────────────
public class CreatePurchaseOrderCommandHandler : IRequestHandler<CreatePurchaseOrderCommand, Guid>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public CreatePurchaseOrderCommandHandler(IAppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Guid> Handle(CreatePurchaseOrderCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.Role is UserRole.ReadOnly)
            throw new UnauthorizedAccessException("Read-only users cannot create purchase orders.");

        // Validate supplier
        var supplierActive = await _db.Suppliers
            .AnyAsync(s => s.Id == request.SupplierId && s.IsActive, cancellationToken);
        if (!supplierActive)
            throw new KeyNotFoundException("Supplier not found or is inactive.");

        // Validate warehouse
        var warehouseActive = await _db.Warehouses
            .AnyAsync(w => w.Id == request.WarehouseId && w.IsActive, cancellationToken);
        if (!warehouseActive)
            throw new KeyNotFoundException("Warehouse not found or is inactive.");

        // Validate all products exist and are active
        var productIds = request.Lines.Select(l => l.ProductId).Distinct().ToList();
        var validProductIds = await _db.Products
            .Where(p => productIds.Contains(p.Id) && p.IsActive)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        var missingProducts = productIds.Except(validProductIds).ToList();
        if (missingProducts.Count > 0)
            throw new KeyNotFoundException(
                $"Products not found or inactive: {string.Join(", ", missingProducts)}");

        var po = new PurchaseOrder
        {
            Id = Guid.NewGuid(),
            SupplierId = request.SupplierId,
            WarehouseId = request.WarehouseId,
            Status = PurchaseOrderStatus.Draft,
            ExpectedDeliveryDate = request.ExpectedDeliveryDate,
            Notes = request.Notes?.Trim(),
            CreatedByUserId = _currentUser.UserId
        };
        _db.PurchaseOrders.Add(po);

        foreach (var line in request.Lines)
        {
            _db.PurchaseOrderLines.Add(new PurchaseOrderLine
            {
                Id = Guid.NewGuid(),
                PurchaseOrderId = po.Id,
                ProductId = line.ProductId,
                QuantityOrdered = line.Quantity,
                QuantityReceived = 0,
                UnitCost = line.UnitCost
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
        return po.Id;
    }
}
