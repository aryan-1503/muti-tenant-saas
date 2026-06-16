using InventoryManager.Application.Features.Reports.GetDashboardSummary;
using InventoryManager.Application.Features.Reports.GetLowStockReport;
using InventoryManager.Application.Features.Reports.GetMovementHistoryReport;
using InventoryManager.Application.Features.Reports.GetStockLevelReport;
using InventoryManager.Application.Features.Reports.GetStockValuationReport;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManager.API.Controllers;

/// <summary>
/// Reporting endpoints — read-only, all tenant-scoped via global query filters.
///
/// Dashboard:   One-call KPI summary (stock health, POs, transfers, low-stock alerts).
/// Reports:
///   Stock Level       — product × warehouse matrix with valuation
///   Low Stock         — items at/below reorder point, sorted by urgency
///   Movement History  — full audit trail over a date range (max 366 days)
///   Stock Valuation   — financial view: cost vs sell value by category and warehouse
/// </summary>
[ApiController]
[Route("api/reports")]
[Authorize]
public class ReportsController : ControllerBase
{
    private readonly IMediator _mediator;
    public ReportsController(IMediator mediator) => _mediator = mediator;

    // ─── GET /api/reports/dashboard ───────────────────────────────────────────
    /// <summary>
    /// Full dashboard KPI summary: stock health, procurement, transfers,
    /// pending counts, notifications, recent movements, top low-stock items.
    /// </summary>
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(DashboardSummaryDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Dashboard(CancellationToken ct) =>
        Ok(await _mediator.Send(new GetDashboardSummaryQuery(), ct));

    // ─── GET /api/reports/stock-levels ───────────────────────────────────────
    /// <summary>
    /// Full stock level report — all products × warehouses with cost valuation.
    /// Filter by warehouseId or categoryId for a focused view.
    /// </summary>
    [HttpGet("stock-levels")]
    [ProducesResponseType(typeof(StockLevelReportDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> StockLevels(
        [FromQuery] Guid? warehouseId,
        [FromQuery] Guid? categoryId,
        CancellationToken ct = default) =>
        Ok(await _mediator.Send(new GetStockLevelReportQuery(warehouseId, categoryId), ct));

    // ─── GET /api/reports/low-stock ──────────────────────────────────────────
    /// <summary>
    /// Items at or below their reorder point.
    /// Sorted by urgency: OutOfStock → Critical → LowStock.
    /// Use criticalOnly=true to see only items at/below minimum stock level.
    /// </summary>
    [HttpGet("low-stock")]
    [ProducesResponseType(typeof(LowStockReportDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> LowStock(
        [FromQuery] Guid? warehouseId,
        [FromQuery] Guid? categoryId,
        [FromQuery] bool criticalOnly = false,
        CancellationToken ct = default) =>
        Ok(await _mediator.Send(new GetLowStockReportQuery(warehouseId, categoryId, criticalOnly), ct));

    // ─── GET /api/reports/movement-history ───────────────────────────────────
    /// <summary>
    /// Date-ranged stock movement audit trail. Max range: 366 days.
    /// Returns stock-in vs stock-out totals broken down by movement type.
    /// </summary>
    [HttpGet("movement-history")]
    [ProducesResponseType(typeof(MovementHistoryReportDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> MovementHistory(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] Guid? warehouseId,
        [FromQuery] Guid? productId,
        [FromQuery] Guid? categoryId,
        CancellationToken ct = default) =>
        Ok(await _mediator.Send(
            new GetMovementHistoryReportQuery(from, to, warehouseId, productId, categoryId), ct));

    // ─── GET /api/reports/valuation ──────────────────────────────────────────
    /// <summary>
    /// Inventory valuation: cost value, sell value, and potential gross profit.
    /// Grouped by category and warehouse. Useful for period-end reporting.
    /// </summary>
    [HttpGet("valuation")]
    [ProducesResponseType(typeof(StockValuationReportDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Valuation(
        [FromQuery] Guid? warehouseId,
        [FromQuery] Guid? categoryId,
        CancellationToken ct = default) =>
        Ok(await _mediator.Send(new GetStockValuationReportQuery(warehouseId, categoryId), ct));
}
