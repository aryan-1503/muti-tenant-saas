using InventoryManager.Application.Features.StockCounts.CreateStockCount;
using InventoryManager.Application.Features.StockCounts.GetStockCount;
using InventoryManager.Application.Features.StockCounts.ListStockCounts;
using InventoryManager.Application.Features.StockCounts.ReviewStockCount;
using InventoryManager.Application.Features.StockCounts.SubmitCountedQuantities;
using InventoryManager.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManager.API.Controllers;

/// <summary>
/// Physical stock count workflow:
///
///   1. POST /api/stock-counts              — Manager creates count session (snapshots system qty)
///   2. GET  /api/stock-counts/{id}          — Staff view items to count (blind: no expected qty shown)
///   3. POST /api/stock-counts/{id}/submit   — Staff submit their physical counts
///   4. POST /api/stock-counts/{id}/review   — Manager approves/rejects variances, posts adjustments
///
/// IsBlindCount=true (default) hides SystemQuantity from staff to prevent anchoring bias.
/// After the review, CountAdjustment movements are posted to the audit trail.
/// </summary>
[ApiController]
[Route("api/stock-counts")]
[Authorize]
public class StockCountsController : ControllerBase
{
    private readonly IMediator _mediator;
    public StockCountsController(IMediator mediator) => _mediator = mediator;

    // GET /api/stock-counts
    [HttpGet]
    [ProducesResponseType(typeof(StockCountListResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] StockCountStatus? status,
        [FromQuery] Guid? warehouseId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default) =>
        Ok(await _mediator.Send(new ListStockCountsQuery(status, warehouseId, page, pageSize), ct));

    // GET /api/stock-counts/{id}
    /// <summary>
    /// View count detail. Use showSystemQuantities=true to reveal expected quantities
    /// (automatically revealed for Submitted/Reviewing/Completed counts).
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(StockCountDetailDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(
        Guid id,
        [FromQuery] bool showSystemQuantities = false,
        CancellationToken ct = default) =>
        Ok(await _mediator.Send(new GetStockCountQuery(id, showSystemQuantities), ct));

    // POST /api/stock-counts
    /// <summary>
    /// Create a stock count session. Snapshots current system quantities for all
    /// active products at the warehouse (optionally filtered by category).
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(
        [FromBody] CreateStockCountCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(Get), new { id }, new { id });
    }

    // POST /api/stock-counts/{id}/submit
    /// <summary>
    /// Staff submit counted quantities for all items. All items must be provided
    /// (enter 0 for products not found). Transitions count to Submitted.
    /// </summary>
    [HttpPost("{id:guid}/submit")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Submit(
        Guid id,
        [FromBody] SubmitCountedQuantitiesRequest body,
        CancellationToken ct)
    {
        await _mediator.Send(new SubmitCountedQuantitiesCommand(id, body.Items), ct);
        return NoContent();
    }

    // POST /api/stock-counts/{id}/review
    /// <summary>
    /// Manager reviews variances and approves/rejects items.
    /// Approved items with non-zero variance have CountAdjustment movements posted.
    /// Count completes when all items are approved.
    /// </summary>
    [HttpPost("{id:guid}/review")]
    [ProducesResponseType(typeof(ReviewStockCountResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> Review(
        Guid id,
        [FromBody] ReviewStockCountRequest body,
        CancellationToken ct)
    {
        var result = await _mediator.Send(new ReviewStockCountCommand(id, body.ReviewedItems), ct);
        return Ok(result);
    }
}

public record SubmitCountedQuantitiesRequest(List<CountedItem> Items);
public record ReviewStockCountRequest(List<ReviewedItem> ReviewedItems);
