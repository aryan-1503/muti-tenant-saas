using InventoryManager.Application.Features.Transfers.ConfirmTransferReceipt;
using InventoryManager.Application.Features.Transfers.CreateTransfer;
using InventoryManager.Application.Features.Transfers.GetTransfer;
using InventoryManager.Application.Features.Transfers.ListTransfers;
using InventoryManager.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManager.API.Controllers;

/// <summary>
/// Stock transfer operations — move stock between warehouses.
///
/// Two-step workflow:
/// 1. POST /api/transfers           — creates AND dispatches (status = InTransit)
///                                    Source stock is deducted immediately.
/// 2. POST /api/transfers/{id}/confirm-receipt — destination confirms arrival.
///                                    In-transit cleared, stock credited at destination.
/// </summary>
[ApiController]
[Route("api/transfers")]
[Authorize]
public class TransfersController : ControllerBase
{
    private readonly IMediator _mediator;

    public TransfersController(IMediator mediator) => _mediator = mediator;

    // ─── GET /api/transfers ───────────────────────────────────────────────────
    /// <summary>
    /// List transfers. Filter by status (Pending/InTransit/Completed/Cancelled)
    /// or by warehouseId (returns transfers where warehouse is source OR destination).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(TransferListResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] TransferStatus? status,
        [FromQuery] Guid? warehouseId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default) =>
        Ok(await _mediator.Send(new ListTransfersQuery(status, warehouseId, page, pageSize), ct));

    // ─── GET /api/transfers/{id} ──────────────────────────────────────────────
    /// <summary>Full transfer detail with all product lines and shortfall analysis.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(TransferDetailDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) =>
        Ok(await _mediator.Send(new GetTransferQuery(id), ct));

    // ─── POST /api/transfers ──────────────────────────────────────────────────
    /// <summary>
    /// Create and dispatch a stock transfer.
    /// Source stock is deducted immediately. Destination shows stock as InTransit.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(
        [FromBody] CreateTransferCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(Get), new { id }, new { id });
    }

    // ─── POST /api/transfers/{id}/confirm-receipt ─────────────────────────────
    /// <summary>
    /// Destination warehouse confirms receipt of goods.
    /// Provide actual quantities received for each product line.
    /// Quantities can be less than requested (shortfall/damage in transit).
    /// </summary>
    [HttpPost("{id:guid}/confirm-receipt")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ConfirmReceipt(
        Guid id,
        [FromBody] ConfirmReceiptRequest body,
        CancellationToken ct)
    {
        await _mediator.Send(
            new ConfirmTransferReceiptCommand(id, body.ReceivedLines, body.Notes), ct);
        return NoContent();
    }
}

/// <summary>POST body for confirm-receipt — id comes from route.</summary>
public record ConfirmReceiptRequest(
    List<TransferReceiptLine> ReceivedLines,
    string? Notes
);
