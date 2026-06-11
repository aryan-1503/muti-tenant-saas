using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventoryManager.Application.Features.Suppliers.ListSuppliers;

public record ListSuppliersQuery(bool IncludeInactive = false) : IRequest<List<SupplierDto>>;

public record SupplierDto(
    Guid Id,
    string Name,
    string? ContactName,
    string? Email,
    string? Phone,
    string? Address,
    int? LeadTimeDays,
    string? PaymentTerms,
    string? Notes,
    bool IsActive,
    int TotalOrders,
    DateTime CreatedAt
);

public class ListSuppliersQueryHandler : IRequestHandler<ListSuppliersQuery, List<SupplierDto>>
{
    private readonly IAppDbContext _db;
    public ListSuppliersQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<SupplierDto>> Handle(
        ListSuppliersQuery request, CancellationToken cancellationToken)
    {
        var query = _db.Suppliers.AsQueryable();
        if (!request.IncludeInactive)
            query = query.Where(s => s.IsActive);

        return await query
            .OrderBy(s => s.Name)
            .Select(s => new SupplierDto(
                s.Id,
                s.Name,
                s.ContactName,
                s.Email,
                s.Phone,
                s.Address,
                s.LeadTimeDays,
                s.PaymentTerms,
                s.Notes,
                s.IsActive,
                s.PurchaseOrders.Count,
                s.CreatedAt
            ))
            .ToListAsync(cancellationToken);
    }
}
