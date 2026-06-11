using InventoryManager.Application.Features.PurchaseOrders.ApprovePurchaseOrder;
using InventoryManager.Application.Features.PurchaseOrders.CreatePurchaseOrder;
using InventoryManager.Application.Features.PurchaseOrders.GetPurchaseOrder;
using InventoryManager.Application.Features.PurchaseOrders.ListPurchaseOrders;
using InventoryManager.Application.Features.PurchaseOrders.ReceiveGoodsNote;
using InventoryManager.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManager.API.Controllers;

/// <summary>
/// Procurement workflow:
///   1. POST /api/purchase-orders            — create Draft PO
///   2. POST /api/purchase-orders/{id}/approve — send to supplier (Draft → Sent/Confirmed)
///   3. POST /api/purchase-orders/{id}/receive — record GRN when goods arrive
///
/// Each GRN (step 3) immediately updates StockLevels and writes GoodsIn movements.
/// The PO status auto-updates to PartiallyReceived or FullyReceived.
/// </summary>
[ApiController]
[Route("api/purchase-orders")]
[Authorize]
public class PurchaseOrdersController : ControllerBase
{
    private readonly IMediator _mediator;
    public PurchaseOrdersController(IMediator mediator) => _mediator = mediator;

    // GET /api/purchase-orders
    [HttpGet]
    [ProducesResponseType(typeof(PurchaseOrderListResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] PurchaseOrderStatus? status,
        [FromQuery] Guid? supplierId,
        [FromQuery] Guid? warehouseId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default) =>
        Ok(await _mediator.Send(
            new ListPurchaseOrdersQuery(status, supplierId, warehouseId, page, pageSize), ct));

    // GET /api/purchase-orders/{id}
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PurchaseOrderDetailDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) =>
        Ok(await _mediator.Send(new GetPurchaseOrderQuery(id), ct));

    // POST /api/purchase-orders
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(
        [FromBody] CreatePurchaseOrderCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(Get), new { id }, new { id });
    }

    // POST /api/purchase-orders/{id}/approve
    /// <summary>
    /// Send the PO to the supplier. Transitions Draft → Sent.
    /// Pass markAsConfirmed=true to go directly to Confirmed status.
    /// </summary>
    [HttpPost("{id:guid}/approve")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Approve(
        Guid id,
        [FromQuery] bool markAsConfirmed = false,
        CancellationToken ct = default)
    {
        await _mediator.Send(new ApprovePurchaseOrderCommand(id, markAsConfirmed), ct);
        return NoContent();
    }

    // POST /api/purchase-orders/{id}/receive
    /// <summary>
    /// Record a Goods Receipt Note (GRN) — goods physically arrived at warehouse.
    /// Immediately updates stock levels and writes GoodsIn movements.
    /// Supports partial delivery — call again for subsequent deliveries.
    /// </summary>
    [HttpPost("{id:guid}/receive")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Receive(
        Guid id,
        [FromBody] ReceiveGoodsNoteRequest body,
        CancellationToken ct)
    {
        var grnId = await _mediator.Send(
            new ReceiveGoodsNoteCommand(id, body.WarehouseId, body.Lines, body.Notes), ct);
        return Ok(new { grnId, message = "Goods receipt recorded successfully." });
    }
}

public record ReceiveGoodsNoteRequest(
    Guid WarehouseId,
    List<GrnLineInput> Lines,
    string? Notes
);
