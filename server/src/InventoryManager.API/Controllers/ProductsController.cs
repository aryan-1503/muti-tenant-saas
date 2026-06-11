using InventoryManager.Application.Features.Products.CreateProduct;
using InventoryManager.Application.Features.Products.DeleteProduct;
using InventoryManager.Application.Features.Products.GetProduct;
using InventoryManager.Application.Features.Products.ImportProductsCsv;
using InventoryManager.Application.Features.Products.ListProducts;
using InventoryManager.Application.Features.Products.UpdateProduct;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryManager.API.Controllers;

[ApiController]
[Route("api/products")]
[Authorize]
public class ProductsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ProductsController(IMediator mediator) => _mediator = mediator;

    // ─── GET /api/products ────────────────────────────────────────────────────
    /// <summary>Paginated, filterable product list.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ProductListItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] string? search,
        [FromQuery] Guid? categoryId,
        [FromQuery] bool? isActive,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default) =>
        Ok(await _mediator.Send(new ListProductsQuery(search, categoryId, isActive, page, pageSize), ct));

    // ─── GET /api/products/{id} ───────────────────────────────────────────────
    /// <summary>Full product detail including stock by warehouse.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ProductDetailDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) =>
        Ok(await _mediator.Send(new GetProductQuery(id), ct));

    // ─── POST /api/products ───────────────────────────────────────────────────
    /// <summary>Create a new product in the catalogue.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateProductCommand command, CancellationToken ct)
    {
        var id = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(Get), new { id }, new { id });
    }

    // ─── PUT /api/products/{id} ───────────────────────────────────────────────
    /// <summary>Update product details. SKU code cannot be changed.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Update(
        Guid id, [FromBody] UpdateProductRequest body, CancellationToken ct)
    {
        await _mediator.Send(new UpdateProductCommand(
            id,
            body.Name,
            body.Description,
            body.Barcode,
            body.CategoryId,
            body.UnitOfMeasure,
            body.CostPrice,
            body.SellPrice,
            body.ReorderPoint,
            body.ReorderQuantity,
            body.MinStockLevel,
            body.IsActive
        ), ct);
        return NoContent();
    }

    // ─── DELETE /api/products/{id} ────────────────────────────────────────────
    /// <summary>Soft-deactivate a product. Blocked if stock on hand exists.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new DeleteProductCommand(id), ct);
        return NoContent();
    }

    // ─── POST /api/products/import ────────────────────────────────────────────
    /// <summary>
    /// Import products from a CSV file.
    /// Returns per-row errors — valid rows are imported even if some rows fail.
    /// Expected columns: Name, SkuCode, Description, Barcode, CategoryName,
    ///                   UnitOfMeasure, CostPrice, SellPrice, ReorderPoint,
    ///                   ReorderQuantity, MinStockLevel
    /// </summary>
    [HttpPost("import")]
    [ProducesResponseType(typeof(ImportProductsResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> Import(
        IFormFile file, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new ImportProductsCsvCommand(file.OpenReadStream(), file.FileName), ct);
        return Ok(result);
    }
}

/// <summary>PUT request body — id comes from route.</summary>
public record UpdateProductRequest(
    string Name,
    string? Description,
    string? Barcode,
    Guid? CategoryId,
    string UnitOfMeasure,
    decimal CostPrice,
    decimal? SellPrice,
    decimal ReorderPoint,
    decimal ReorderQuantity,
    decimal MinStockLevel,
    bool IsActive
);
