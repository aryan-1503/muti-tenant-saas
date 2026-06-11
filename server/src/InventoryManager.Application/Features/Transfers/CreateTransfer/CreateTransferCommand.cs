using FluentValidation;
using InventoryManager.Domain.Entities;
using InventoryManager.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventoryManager.Application.Features.Transfers.CreateTransfer;

// ─── Command ──────────────────────────────────────────────────────────────────
/// <summary>
/// Creates a stock transfer between two warehouses and immediately dispatches it.
/// 
/// WHAT HAPPENS IN ONE TRANSACTION:
/// 1. Validate source has sufficient stock for every line.
/// 2. Create the StockTransfer header + lines.
/// 3. For each line:
///    a. Deduct QuantityOnHand from source warehouse StockLevel.
///    b. Add QuantityInTransit to destination warehouse StockLevel (upsert).
///    c. Write a TransferOut StockMovement on the source.
/// 4. Set Transfer status = InTransit.
///
/// WHY: Stock is deducted immediately at dispatch so it cannot be sold or
/// re-allocated while it is physically on a truck/in transit.
/// </summary>
public record CreateTransferCommand(
    Guid FromWarehouseId,
    Guid ToWarehouseId,
    List<TransferLineInput> Lines,
    string? Notes
) : IRequest<Guid>;

public record TransferLineInput(Guid ProductId, decimal Quantity);

// ─── Validator ────────────────────────────────────────────────────────────────
public class CreateTransferCommandValidator : AbstractValidator<CreateTransferCommand>
{
    public CreateTransferCommandValidator()
    {
        RuleFor(x => x.FromWarehouseId).NotEmpty();
        RuleFor(x => x.ToWarehouseId).NotEmpty();
        RuleFor(x => x.FromWarehouseId)
            .NotEqual(x => x.ToWarehouseId)
            .WithMessage("Source and destination warehouse must be different.");
        RuleFor(x => x.Lines)
            .NotEmpty().WithMessage("A transfer must have at least one product line.")
            .Must(lines => lines.All(l => l.Quantity > 0))
            .WithMessage("All line quantities must be greater than zero.");
        RuleFor(x => x.Notes).MaximumLength(1000).When(x => x.Notes is not null);
    }
}

// ─── Handler ──────────────────────────────────────────────────────────────────
public class CreateTransferCommandHandler : IRequestHandler<CreateTransferCommand, Guid>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public CreateTransferCommandHandler(IAppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Guid> Handle(CreateTransferCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.Role is UserRole.ReadOnly)
            throw new UnauthorizedAccessException("Read-only users cannot create stock transfers.");

        // ── Validate warehouses ───────────────────────────────────────────────
        var fromWarehouse = await _db.Warehouses
            .FirstOrDefaultAsync(w => w.Id == request.FromWarehouseId && w.IsActive, cancellationToken)
            ?? throw new KeyNotFoundException("Source warehouse not found or inactive.");

        var toWarehouse = await _db.Warehouses
            .FirstOrDefaultAsync(w => w.Id == request.ToWarehouseId && w.IsActive, cancellationToken)
            ?? throw new KeyNotFoundException("Destination warehouse not found or inactive.");

        // ── Load source stock levels for all lines ────────────────────────────
        var productIds = request.Lines.Select(l => l.ProductId).Distinct().ToList();

        var sourceStockLevels = await _db.StockLevels
            .Include(sl => sl.Product)
            .Where(sl => sl.WarehouseId == request.FromWarehouseId
                      && productIds.Contains(sl.ProductId))
            .ToListAsync(cancellationToken);

        // ── Validate sufficient stock for every line ──────────────────────────
        var insufficientLines = new List<string>();
        foreach (var line in request.Lines)
        {
            var sl = sourceStockLevels.FirstOrDefault(s => s.ProductId == line.ProductId);
            if (sl is null)
            {
                insufficientLines.Add(
                    $"Product '{line.ProductId}' has no stock record at the source warehouse.");
                continue;
            }
            if (sl.QuantityOnHand < line.Quantity)
                insufficientLines.Add(
                    $"'{sl.Product.Name}' (SKU: {sl.Product.SkuCode}): requested {line.Quantity} " +
                    $"but only {sl.QuantityOnHand} available.");
        }

        if (insufficientLines.Count > 0)
            throw new InvalidOperationException(
                "Insufficient stock for transfer:\n" + string.Join("\n", insufficientLines));

        // ── Load or create destination stock levels ───────────────────────────
        var destStockLevels = await _db.StockLevels
            .Where(sl => sl.WarehouseId == request.ToWarehouseId
                      && productIds.Contains(sl.ProductId))
            .ToDictionaryAsync(sl => sl.ProductId, cancellationToken);

        // ── Create Transfer header ────────────────────────────────────────────
        var transfer = new StockTransfer
        {
            Id = Guid.NewGuid(),
            FromWarehouseId = request.FromWarehouseId,
            ToWarehouseId = request.ToWarehouseId,
            Status = TransferStatus.InTransit,
            Notes = request.Notes?.Trim(),
            CreatedByUserId = _currentUser.UserId,
            DispatchedAt = DateTime.UtcNow
        };
        _db.StockTransfers.Add(transfer);

        // ── Process each line ─────────────────────────────────────────────────
        foreach (var line in request.Lines)
        {
            var sourceLevel = sourceStockLevels.First(s => s.ProductId == line.ProductId);

            // Add transfer line
            _db.StockTransferLines.Add(new StockTransferLine
            {
                Id = Guid.NewGuid(),
                TransferId = transfer.Id,
                ProductId = line.ProductId,
                QuantityRequested = line.Quantity
            });

            // Deduct from source
            sourceLevel.QuantityOnHand -= line.Quantity;

            // Add to destination in-transit (upsert)
            if (destStockLevels.TryGetValue(line.ProductId, out var destLevel))
            {
                destLevel.QuantityInTransit += line.Quantity;
            }
            else
            {
                var newDestLevel = new StockLevel
                {
                    Id = Guid.NewGuid(),
                    ProductId = line.ProductId,
                    WarehouseId = request.ToWarehouseId,
                    QuantityOnHand = 0,
                    QuantityInTransit = line.Quantity
                };
                _db.StockLevels.Add(newDestLevel);
                destStockLevels[line.ProductId] = newDestLevel;
            }

            // Write TransferOut audit movement on source
            _db.StockMovements.Add(new StockMovement
            {
                Id = Guid.NewGuid(),
                ProductId = line.ProductId,
                WarehouseId = request.FromWarehouseId,
                MovementType = MovementType.TransferOut,
                QuantityChange = -line.Quantity,
                QuantityAfter = sourceLevel.QuantityOnHand,
                ReferenceType = "Transfer",
                ReferenceId = transfer.Id,
                Notes = $"Transfer to {toWarehouse.Name}",
                CreatedByUserId = _currentUser.UserId
            });
        }

        // ── Notify destination warehouse managers ─────────────────────────────
        var managers = await _db.Users
            .Where(u => u.IsActive &&
                        (u.Role == UserRole.Admin || u.Role == UserRole.Manager))
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);

        foreach (var userId in managers)
        {
            _db.Notifications.Add(new Notification
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Type = NotificationType.TransferPending,
                Message = $"Stock transfer in transit from '{fromWarehouse.Name}' to '{toWarehouse.Name}' " +
                          $"({request.Lines.Count} product(s)). Confirm receipt when goods arrive.",
                ReferenceType = "Transfer",
                ReferenceId = transfer.Id,
                IsRead = false
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
        return transfer.Id;
    }
}
