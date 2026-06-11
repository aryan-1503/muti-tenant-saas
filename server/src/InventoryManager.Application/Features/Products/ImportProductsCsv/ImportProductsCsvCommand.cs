using CsvHelper;
using CsvHelper.Configuration;
using FluentValidation;
using InventoryManager.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace InventoryManager.Application.Features.Products.ImportProductsCsv;

// ─── Command ──────────────────────────────────────────────────────────────────
/// <summary>
/// Imports products from a CSV file.
/// Row-level error reporting: valid rows are saved, invalid rows are skipped and reported.
/// The import is NON-transactional per-row — a bad row doesn't roll back good rows.
/// </summary>
public record ImportProductsCsvCommand(Stream FileStream, string FileName) : IRequest<ImportProductsResult>;

// ─── Result ───────────────────────────────────────────────────────────────────
public record ImportProductsResult(
    int ImportedCount,
    int SkippedCount,
    List<ImportRowError> Errors
);

public record ImportRowError(int RowNumber, string SkuCode, string Error);

// ─── CSV Row Mapping ──────────────────────────────────────────────────────────
/// <summary>
/// Maps CSV columns to the import DTO.
/// Expected headers (case-insensitive):
///   Name, SkuCode, Description, Barcode, CategoryName, UnitOfMeasure,
///   CostPrice, SellPrice, ReorderPoint, ReorderQuantity, MinStockLevel
/// </summary>
public class ProductCsvRow
{
    public string Name { get; set; } = string.Empty;
    public string SkuCode { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Barcode { get; set; }
    public string? CategoryName { get; set; }
    public string UnitOfMeasure { get; set; } = "Each";
    public decimal CostPrice { get; set; }
    public decimal? SellPrice { get; set; }
    public decimal ReorderPoint { get; set; }
    public decimal ReorderQuantity { get; set; }
    public decimal MinStockLevel { get; set; }
}

public sealed class ProductCsvRowMap : ClassMap<ProductCsvRow>
{
    public ProductCsvRowMap()
    {
        Map(m => m.Name).Name("Name");
        Map(m => m.SkuCode).Name("SkuCode");
        Map(m => m.Description).Name("Description").Optional();
        Map(m => m.Barcode).Name("Barcode").Optional();
        Map(m => m.CategoryName).Name("CategoryName").Optional();
        Map(m => m.UnitOfMeasure).Name("UnitOfMeasure");
        Map(m => m.CostPrice).Name("CostPrice");
        Map(m => m.SellPrice).Name("SellPrice").Optional();
        Map(m => m.ReorderPoint).Name("ReorderPoint");
        Map(m => m.ReorderQuantity).Name("ReorderQuantity");
        Map(m => m.MinStockLevel).Name("MinStockLevel");
    }
}

// ─── Handler ──────────────────────────────────────────────────────────────────
public class ImportProductsCsvCommandHandler
    : IRequestHandler<ImportProductsCsvCommand, ImportProductsResult>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public ImportProductsCsvCommandHandler(IAppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ImportProductsResult> Handle(
        ImportProductsCsvCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.Role is Domain.Enums.UserRole.ReadOnly)
            throw new UnauthorizedAccessException("Read-only users cannot import products.");

        if (request.FileStream.Length == 0)
            throw new ArgumentException("Uploaded CSV file is empty.");

        if (!request.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Only CSV files are accepted.");

        // Pre-load existing SKUs and categories for bulk comparison
        var existingSkus = await _db.Products
            .Select(p => p.SkuCode.ToLower())
            .ToHashSetAsync(cancellationToken);

        var categoryLookup = await _db.Categories
            .ToDictionaryAsync(c => c.Name.ToLower(), c => c.Id, cancellationToken);

        var errors = new List<ImportRowError>();
        var toInsert = new List<Product>();
        var rowNumber = 1; // Header is row 0, data starts at 1

        using var stream = request.FileStream;
        using var reader = new StreamReader(stream);

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            HeaderValidated = null,         // Don't throw on missing optional headers
            MissingFieldFound = null,       // Don't throw on missing optional fields
            TrimOptions = TrimOptions.Trim,
            PrepareHeaderForMatch = args => args.Header.ToLower()
        };

        using var csv = new CsvReader(reader, config);
        csv.Context.RegisterClassMap<ProductCsvRowMap>();

        await csv.ReadAsync();
        csv.ReadHeader();

        while (await csv.ReadAsync())
        {
            rowNumber++;
            ProductCsvRow row;

            try
            {
                row = csv.GetRecord<ProductCsvRow>()!;
            }
            catch (Exception ex)
            {
                errors.Add(new ImportRowError(rowNumber, "?", $"Could not parse row: {ex.Message}"));
                continue;
            }

            // ── Row-level validation ──────────────────────────────────────────
            if (string.IsNullOrWhiteSpace(row.Name))
            {
                errors.Add(new ImportRowError(rowNumber, row.SkuCode, "Name is required."));
                continue;
            }

            if (string.IsNullOrWhiteSpace(row.SkuCode))
            {
                errors.Add(new ImportRowError(rowNumber, row.SkuCode, "SKU code is required."));
                continue;
            }

            var normalizedSku = row.SkuCode.Trim().ToUpper();

            // Check against DB and already-queued inserts in this batch
            if (existingSkus.Contains(normalizedSku.ToLower()) ||
                toInsert.Any(p => p.SkuCode == normalizedSku))
            {
                errors.Add(new ImportRowError(rowNumber, row.SkuCode,
                    $"SKU '{normalizedSku}' already exists or is duplicated in this file."));
                continue;
            }

            if (row.CostPrice < 0)
            {
                errors.Add(new ImportRowError(rowNumber, row.SkuCode, "Cost price cannot be negative."));
                continue;
            }

            // ── Category lookup ───────────────────────────────────────────────
            Guid? categoryId = null;
            if (!string.IsNullOrWhiteSpace(row.CategoryName))
            {
                if (categoryLookup.TryGetValue(row.CategoryName.Trim().ToLower(), out var catId))
                    categoryId = catId;
                else
                {
                    // Create the category on-the-fly during import
                    var newCategory = new Category
                    {
                        Id = Guid.NewGuid(),
                        Name = row.CategoryName.Trim()
                    };
                    _db.Categories.Add(newCategory);
                    categoryLookup[row.CategoryName.Trim().ToLower()] = newCategory.Id;
                    categoryId = newCategory.Id;
                }
            }

            // ── Queue for bulk insert ─────────────────────────────────────────
            toInsert.Add(new Product
            {
                Id = Guid.NewGuid(),
                Name = row.Name.Trim(),
                SkuCode = normalizedSku,
                Description = row.Description?.Trim(),
                Barcode = row.Barcode?.Trim(),
                CategoryId = categoryId,
                UnitOfMeasure = string.IsNullOrWhiteSpace(row.UnitOfMeasure) ? "Each" : row.UnitOfMeasure.Trim(),
                CostPrice = row.CostPrice,
                SellPrice = row.SellPrice,
                ReorderPoint = row.ReorderPoint,
                ReorderQuantity = row.ReorderQuantity > 0 ? row.ReorderQuantity : 1,
                MinStockLevel = row.MinStockLevel,
                IsActive = true
            });

            existingSkus.Add(normalizedSku.ToLower()); // Prevent duplicates in same file
        }

        // ── Bulk insert all valid rows in one SaveChanges ─────────────────────
        if (toInsert.Count > 0)
        {
            _db.Products.AddRange(toInsert);
            await _db.SaveChangesAsync(cancellationToken);
        }

        return new ImportProductsResult(
            ImportedCount: toInsert.Count,
            SkippedCount: errors.Count,
            Errors: errors
        );
    }
}
