using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventoryManager.Application.Features.Products.UpdateProduct;

// ─── Command ──────────────────────────────────────────────────────────────────
public record UpdateProductCommand(
    Guid Id,
    string Name,
    string? Description,
    string? Barcode,
    Guid? CategoryId,
    string UnitOfMeasure,
    decimal CostPrice,
    decimal? SellPrice,
    decimal ReorderPoint,
    decimal ReorderQuantity,
    decimal MinStockLevel,
    bool IsActive
) : IRequest;

// ─── Validator ────────────────────────────────────────────────────────────────
public class UpdateProductCommandValidator : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.UnitOfMeasure).NotEmpty().MaximumLength(50);
        RuleFor(x => x.CostPrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.SellPrice).GreaterThanOrEqualTo(0).When(x => x.SellPrice.HasValue);
        RuleFor(x => x.ReorderPoint).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ReorderQuantity).GreaterThan(0);
        RuleFor(x => x.MinStockLevel).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Description).MaximumLength(2000).When(x => x.Description is not null);
        RuleFor(x => x.Barcode).MaximumLength(100).When(x => x.Barcode is not null);
    }
}

// ─── Handler ──────────────────────────────────────────────────────────────────
public class UpdateProductCommandHandler : IRequestHandler<UpdateProductCommand>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public UpdateProductCommandHandler(IAppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task Handle(UpdateProductCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.Role is Domain.Enums.UserRole.ReadOnly)
            throw new UnauthorizedAccessException("Read-only users cannot update products.");

        var product = await _db.Products
            .FirstOrDefaultAsync(p => p.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Product not found.");

        // Validate CategoryId if changing
        if (request.CategoryId.HasValue && request.CategoryId != product.CategoryId)
        {
            var categoryExists = await _db.Categories
                .AnyAsync(c => c.Id == request.CategoryId.Value, cancellationToken);
            if (!categoryExists)
                throw new KeyNotFoundException("The specified category does not exist.");
        }

        // Note: SKU code is intentionally NOT updatable — it's the permanent identifier
        // used on purchase orders, stock movements, and GRN lines.
        product.Name = request.Name.Trim();
        product.Description = request.Description?.Trim();
        product.Barcode = request.Barcode?.Trim();
        product.CategoryId = request.CategoryId;
        product.UnitOfMeasure = request.UnitOfMeasure.Trim();
        product.CostPrice = request.CostPrice;
        product.SellPrice = request.SellPrice;
        product.ReorderPoint = request.ReorderPoint;
        product.ReorderQuantity = request.ReorderQuantity;
        product.MinStockLevel = request.MinStockLevel;
        product.IsActive = request.IsActive;

        await _db.SaveChangesAsync(cancellationToken);
    }
}
