using InventoryManager.Application.Features.Categories.CreateCategory;
using InventoryManager.Application.Features.Categories.DeleteCategory;
using InventoryManager.Application.Features.Categories.ListCategories;
using InventoryManager.Application.Features.Categories.UpdateCategory;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManager.API.Controllers;

[ApiController]
[Route("api/categories")]
[Authorize]
public class CategoriesController : ControllerBase
{
    private readonly IMediator _mediator;

    public CategoriesController(IMediator mediator) => _mediator = mediator;

    // GET /api/categories
    [HttpGet]
    [ProducesResponseType(typeof(List<CategoryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await _mediator.Send(new ListCategoriesQuery(), ct));

    // POST /api/categories
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateCategoryCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(List), new { }, new { id });
    }

    // PUT /api/categories/{id}
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Update(
        Guid id, [FromBody] UpdateCategoryRequest body, CancellationToken ct)
    {
        await _mediator.Send(new UpdateCategoryCommand(id, body.Name, body.Description), ct);
        return NoContent();
    }

    // DELETE /api/categories/{id}
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new DeleteCategoryCommand(id), ct);
        return NoContent();
    }
}

public record UpdateCategoryRequest(string Name, string? Description);
