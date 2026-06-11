using InventoryManager.Application.Features.Warehouses.CreateWarehouse;
using InventoryManager.Application.Features.Warehouses.DeleteWarehouse;
using InventoryManager.Application.Features.Warehouses.ListWarehouses;
using InventoryManager.Application.Features.Warehouses.UpdateWarehouse;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManager.API.Controllers;

/// <summary>
/// CRUD operations for warehouses.
/// All endpoints are tenant-scoped — global query filters ensure you only see your own warehouses.
/// </summary>
[ApiController]
[Route("api/warehouses")]
[Authorize]
public class WarehousesController : ControllerBase
{
    private readonly IMediator _mediator;

    public WarehousesController(IMediator mediator) => _mediator = mediator;

    // ─── GET /api/warehouses ──────────────────────────────────────────────────
    /// <summary>List all active warehouses. Pass ?includeInactive=true to include deactivated ones.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<WarehouseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] bool includeInactive = false, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new ListWarehousesQuery(includeInactive), ct);
        return Ok(result);
    }

    // ─── POST /api/warehouses ─────────────────────────────────────────────────
    /// <summary>Create a new warehouse. Manager or Admin only.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(
        [FromBody] CreateWarehouseCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(List), new { }, new { id });
    }

    // ─── PUT /api/warehouses/{id} ─────────────────────────────────────────────
    /// <summary>Update a warehouse's name, address, or type. Manager or Admin only.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateWarehouseRequest body,
        CancellationToken ct)
    {
        await _mediator.Send(new UpdateWarehouseCommand(id, body.Name, body.Address, body.Type), ct);
        return NoContent();
    }

    // ─── DELETE /api/warehouses/{id} ──────────────────────────────────────────
    /// <summary>
    /// Soft-deactivate a warehouse. Admin only.
    /// Will fail if the warehouse has any stock on hand — transfer it first.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new DeleteWarehouseCommand(id), ct);
        return NoContent();
    }
}

/// <summary>
/// Request body for PUT — id comes from route, body contains the rest.
/// Keeps the command clean (id not duplicated in body).
/// </summary>
public record UpdateWarehouseRequest(string Name, string? Address, string? Type);
