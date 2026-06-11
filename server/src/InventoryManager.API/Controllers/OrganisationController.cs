using InventoryManager.Application.Features.Organisation.GetProfile;
using InventoryManager.Application.Features.Organisation.UpdateProfile;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManager.API.Controllers;

/// <summary>
/// Manages the organisation (tenant) profile.
/// All endpoints require authentication — you can only manage YOUR own organisation.
/// Tenant isolation is enforced by the global query filter — no TenantId in the URL needed.
/// </summary>
[ApiController]
[Route("api/organisation")]
[Authorize]
public class OrganisationController : ControllerBase
{
    private readonly IMediator _mediator;

    public OrganisationController(IMediator mediator) => _mediator = mediator;

    // ─── GET /api/organisation ────────────────────────────────────────────────
    /// <summary>Get the current organisation's profile.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(OrganisationProfileDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProfile(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetOrganisationProfileQuery(), ct);
        return Ok(result);
    }

    // ─── PUT /api/organisation ────────────────────────────────────────────────
    /// <summary>Update the organisation's name, industry, currency, timezone, or logo. Admin only.</summary>
    [HttpPut]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UpdateProfile(
        [FromBody] UpdateOrganisationProfileCommand command, CancellationToken ct)
    {
        await _mediator.Send(command, ct);
        return NoContent();
    }
}
