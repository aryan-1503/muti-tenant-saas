using InventoryManager.Application.Features.Suppliers.CreateSupplier;
using InventoryManager.Application.Features.Suppliers.DeleteSupplier;
using InventoryManager.Application.Features.Suppliers.ListSuppliers;
using InventoryManager.Application.Features.Suppliers.UpdateSupplier;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManager.API.Controllers;

[ApiController]
[Route("api/suppliers")]
[Authorize]
public class SuppliersController : ControllerBase
{
    private readonly IMediator _mediator;
    public SuppliersController(IMediator mediator) => _mediator = mediator;

    // GET /api/suppliers
    [HttpGet]
    [ProducesResponseType(typeof(List<SupplierDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] bool includeInactive = false, CancellationToken ct = default) =>
        Ok(await _mediator.Send(new ListSuppliersQuery(includeInactive), ct));

    // POST /api/suppliers
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(
        [FromBody] CreateSupplierCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(List), new { }, new { id });
    }

    // PUT /api/suppliers/{id}
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Update(
        Guid id, [FromBody] UpdateSupplierRequest body, CancellationToken ct)
    {
        await _mediator.Send(new UpdateSupplierCommand(
            id, body.Name, body.ContactName, body.Email, body.Phone,
            body.Address, body.LeadTimeDays, body.PaymentTerms, body.Notes, body.IsActive
        ), ct);
        return NoContent();
    }

    // DELETE /api/suppliers/{id}
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new DeleteSupplierCommand(id), ct);
        return NoContent();
    }
}

public record UpdateSupplierRequest(
    string Name,
    string? ContactName,
    string? Email,
    string? Phone,
    string? Address,
    int? LeadTimeDays,
    string? PaymentTerms,
    string? Notes,
    bool IsActive
);
