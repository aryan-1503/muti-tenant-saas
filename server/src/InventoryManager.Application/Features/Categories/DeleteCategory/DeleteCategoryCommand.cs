using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventoryManager.Application.Features.Categories.DeleteCategory;

// ─── Command ──────────────────────────────────────────────────────────────────
/// <summary>
/// Hard-deletes a category ONLY if it has no products assigned.
/// If products exist, the user must re-assign or delete them first.
/// Categories with no products are safe to delete.
/// </summary>
public record DeleteCategoryCommand(Guid Id) : IRequest;

// ─── Handler ──────────────────────────────────────────────────────────────────
public class DeleteCategoryCommandHandler : IRequestHandler<DeleteCategoryCommand>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public DeleteCategoryCommandHandler(IAppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task Handle(DeleteCategoryCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.Role is Domain.Enums.UserRole.ReadOnly or Domain.Enums.UserRole.Staff)
            throw new UnauthorizedAccessException("Only Managers and Admins can delete categories.");

        var category = await _db.Categories
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Category not found.");

        // Block deletion if products are assigned — nullify or re-assign them first
        var productCount = await _db.Products
            .CountAsync(p => p.CategoryId == request.Id && p.IsActive, cancellationToken);

        if (productCount > 0)
            throw new InvalidOperationException(
                $"Cannot delete category '{category.Name}' — it has {productCount} active product(s). " +
                "Please re-assign or delete those products first.");

        _db.Categories.Remove(category);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
