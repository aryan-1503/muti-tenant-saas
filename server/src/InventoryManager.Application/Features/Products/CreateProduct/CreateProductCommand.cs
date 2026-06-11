using FluentValidation;
using InventoryManager.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventoryManager.Application.Features.Products.CreateProduct;

// ─── Command ──────────────────────────────────────────────────────────────────
public record CreateProductCommand(
    string Name,
    string SkuCode,
    string? Description,
    string? Barcode,
    Guid? CategoryId,
    string UnitOfMeasure,
    decimal CostPrice,
    decimal? SellPrice,
    decimal ReorderPoint,
    decimal ReorderQuantity,
    decimal MinStockLevel
) : IRequest<Guid>;

// ─── Validator ────────────────────────────────────────────────────────────────
public class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Product name is required.")
            .MaximumLength(200);

        RuleFor(x => x.SkuCode)
            .NotEmpty().WithMessage("SKU code is required.")
            .MaximumLength(100)
            .Matches(@"^[A-Za-z0-9\-_\.]+$").WithMessage("SKU code may only contain letters, numbers, hyphens, underscores, and dots.");

        RuleFor(x => x.UnitOfMeasure)
            .NotEmpty().WithMessage("Unit of measure is required.")
            .MaximumLength(50);

        RuleFor(x => x.CostPrice)
            .GreaterThanOrEqualTo(0).WithMessage("Cost price cannot be negative.");

        RuleFor(x => x.SellPrice)
            .GreaterThanOrEqualTo(0).When(x => x.SellPrice.HasValue)
            .WithMessage("Sell price cannot be negative.");

        RuleFor(x => x.ReorderPoint)
            .GreaterThanOrEqualTo(0).WithMessage("Reorder point cannot be negative.");

        RuleFor(x => x.ReorderQuantity)
            .GreaterThan(0).WithMessage("Reorder quantity must be greater than zero.");

        RuleFor(x => x.MinStockLevel)
            .GreaterThanOrEqualTo(0).WithMessage("Minimum stock level cannot be negative.");

        RuleFor(x => x.Description).MaximumLength(2000).When(x => x.Description is not null);
        RuleFor(x => x.Barcode).MaximumLength(100).When(x => x.Barcode is not null);
    }
}

// ─── Handler ──────────────────────────────────────────────────────────────────
public class CreateProductCommandHandler : IRequestHandler<CreateProductCommand, Guid>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public CreateProductCommandHandler(IAppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Guid> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.Role is Domain.Enums.UserRole.ReadOnly)
            throw new UnauthorizedAccessException("Read-only users cannot create products.");

        // SKU must be unique within the tenant
        var skuTaken = await _db.Products
            .AnyAsync(p => p.SkuCode.ToLower() == request.SkuCode.ToLower().Trim(), cancellationToken);

        if (skuTaken)
            throw new InvalidOperationException($"SKU '{request.SkuCode}' is already in use.");

        // Validate CategoryId if provided
        if (request.CategoryId.HasValue)
        {
            var categoryExists = await _db.Categories
                .AnyAsync(c => c.Id == request.CategoryId.Value, cancellationToken);
            if (!categoryExists)
                throw new KeyNotFoundException("The specified category does not exist.");
        }

        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            SkuCode = request.SkuCode.Trim().ToUpper(),
            Description = request.Description?.Trim(),
            Barcode = request.Barcode?.Trim(),
            CategoryId = request.CategoryId,
            UnitOfMeasure = request.UnitOfMeasure.Trim(),
            CostPrice = request.CostPrice,
            SellPrice = request.SellPrice,
            ReorderPoint = request.ReorderPoint,
            ReorderQuantity = request.ReorderQuantity,
            MinStockLevel = request.MinStockLevel,
            IsActive = true
        };

        _db.Products.Add(product);
        await _db.SaveChangesAsync(cancellationToken);
        return product.Id;
    }
}
