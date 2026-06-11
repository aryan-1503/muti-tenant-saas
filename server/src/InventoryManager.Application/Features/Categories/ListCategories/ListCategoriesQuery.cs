using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventoryManager.Application.Features.Categories.ListCategories;

// ─── Query ────────────────────────────────────────────────────────────────────
public record ListCategoriesQuery : IRequest<List<CategoryDto>>;

// ─── DTO ─────────────────────────────────────────────────────────────────────
public record CategoryDto(
    Guid Id,
    string Name,
    string? Description,
    int ProductCount,
    DateTime CreatedAt
);

// ─── Handler ──────────────────────────────────────────────────────────────────
public class ListCategoriesQueryHandler : IRequestHandler<ListCategoriesQuery, List<CategoryDto>>
{
    private readonly IAppDbContext _db;

    public ListCategoriesQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<CategoryDto>> Handle(
        ListCategoriesQuery request, CancellationToken cancellationToken) =>
        await _db.Categories
            .OrderBy(c => c.Name)
            .Select(c => new CategoryDto(
                c.Id,
                c.Name,
                c.Description,
                c.Products.Count(p => p.IsActive),
                c.CreatedAt
            ))
            .ToListAsync(cancellationToken);
}
