using InventoryManager.Application.Features.Stock.AdjustStock;
using InventoryManager.Application.Features.Stock.GetMovementHistory;
using InventoryManager.Application.Features.Stock.GetStockLevels;
using InventoryManager.Application.Features.Stock.SetOpeningBalance;
using InventoryManager.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManager.API.Controllers;

/// <summary>
/// Core stock operations — the heart of the inventory management system.
///
/// Every stock mutation (opening balance, adjustment, transfer, GRN) writes a
/// StockMovement record. The movement history is the immutable audit trail.
///
/// Tenant isolation is enforced automatically by the global EF query filters —
/// you only ever see/modify your own tenant's stock.
/// </summary>
[ApiController]
[Route("api/stock")]
[Authorize]
public class StockController : ControllerBase
{
    private readonly IMediator _mediator;

    public StockController(IMediator mediator) => _mediator = mediator;

    // ─── GET /api/stock/levels ────────────────────────────────────────────────
    /// <summary>
    /// Current stock levels — the full product×warehouse matrix.
    /// Filter by warehouse, product, category, or use belowReorderPoint=true
    /// to see only items that need reordering.
    /// </summary>
    [HttpGet("levels")]
    [ProducesResponseType(typeof(List<StockLevelDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStockLevels(
        [FromQuery] Guid? warehouseId,
        [FromQuery] Guid? productId,
        [FromQuery] Guid? categoryId,
        [FromQuery] bool belowReorderPoint = false,
        CancellationToken ct = default) =>
        Ok(await _mediator.Send(
            new GetStockLevelsQuery(warehouseId, productId, categoryId, belowReorderPoint), ct));

    // ─── GET /api/stock/movements ─────────────────────────────────────────────
    /// <summary>
    /// Paginated, filterable stock movement audit trail.
    /// All roles can read movements.
    /// </summary>
    [HttpGet("movements")]
    [ProducesResponseType(typeof(MovementHistoryResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMovementHistory(
        [FromQuery] Guid? productId,
        [FromQuery] Guid? warehouseId,
        [FromQuery] MovementType? movementType,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default) =>
        Ok(await _mediator.Send(
            new GetMovementHistoryQuery(productId, warehouseId, movementType, from, to, page, pageSize), ct));

    // ─── POST /api/stock/opening-balance ─────────────────────────────────────
    /// <summary>
    /// Set the opening stock balance for a product at a warehouse.
    /// Manager or Admin only. Safe to call multiple times — subsequent calls
    /// are corrections and create a corrective audit movement.
    /// </summary>
    [HttpPost("opening-balance")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> SetOpeningBalance(
        [FromBody] SetOpeningBalanceCommand command, CancellationToken ct)
    {
        var movementId = await _mediator.Send(command, ct);
        return Ok(new { movementId, message = "Opening balance set successfully." });
    }

    // ─── POST /api/stock/adjust ───────────────────────────────────────────────
    /// <summary>
    /// Manual stock adjustment (correction, write-off, damage, found stock, etc.).
    /// Requires a reason. Positive quantity = add stock, negative = remove stock.
    /// Cannot reduce stock below zero.
    /// Automatically creates low-stock notifications if reorder point is crossed.
    /// </summary>
    [HttpPost("adjust")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> AdjustStock(
        [FromBody] AdjustStockCommand command, CancellationToken ct)
    {
        var movementId = await _mediator.Send(command, ct);
        return Ok(new { movementId, message = "Stock adjustment applied successfully." });
    }
}
