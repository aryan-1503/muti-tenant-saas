using InventoryManager.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventoryManager.Application.Features.Transfers.GetTransfer;

// ─── Query ────────────────────────────────────────────────────────────────────
public record GetTransferQuery(Guid Id) : IRequest<TransferDetailDto>;

// ─── DTOs ─────────────────────────────────────────────────────────────────────
public record TransferDetailDto(
    Guid Id,
    Guid FromWarehouseId,
    string FromWarehouseName,
    Guid ToWarehouseId,
    string ToWarehouseName,
    TransferStatus Status,
    string StatusLabel,
    string? Notes,
    string CreatedByUserName,
    DateTime CreatedAt,
    DateTime? DispatchedAt,
    DateTime? CompletedAt,
    List<TransferLineDetailDto> Lines
);

public record TransferLineDetailDto(
    Guid ProductId,
    string ProductName,
    string SkuCode,
    string UnitOfMeasure,
    decimal QuantityRequested,
    decimal? QuantityReceived,
    bool HasShortfall,
    decimal Shortfall,
    string? Notes
);

// ─── Handler ──────────────────────────────────────────────────────────────────
public class GetTransferQueryHandler : IRequestHandler<GetTransferQuery, TransferDetailDto>
{
    private readonly IAppDbContext _db;

    public GetTransferQueryHandler(IAppDbContext db) => _db = db;

    public async Task<TransferDetailDto> Handle(
        GetTransferQuery request, CancellationToken cancellationToken)
    {
        var transfer = await _db.StockTransfers
            .Include(t => t.FromWarehouse)
            .Include(t => t.ToWarehouse)
            .Include(t => t.CreatedByUser)
            .Include(t => t.Lines).ThenInclude(l => l.Product)
            .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken)
            ?? throw new KeyNotFoundException("Transfer not found.");

        return new TransferDetailDto(
            transfer.Id,
            transfer.FromWarehouseId,
            transfer.FromWarehouse.Name,
            transfer.ToWarehouseId,
            transfer.ToWarehouse.Name,
            transfer.Status,
            transfer.Status.ToString(),
            transfer.Notes,
            transfer.CreatedByUser.FullName,
            transfer.CreatedAt,
            transfer.DispatchedAt,
            transfer.CompletedAt,
            transfer.Lines.Select(l =>
            {
                var shortfall = l.QuantityReceived.HasValue
                    ? Math.Max(0, l.QuantityRequested - l.QuantityReceived.Value)
                    : 0;
                return new TransferLineDetailDto(
                    l.ProductId,
                    l.Product.Name,
                    l.Product.SkuCode,
                    l.Product.UnitOfMeasure,
                    l.QuantityRequested,
                    l.QuantityReceived,
                    shortfall > 0,
                    shortfall,
                    l.Notes
                );
            }).ToList()
        );
    }
}
