using InventoryManager.Domain.Common;

namespace InventoryManager.Domain.Entities;

public class StockTransferLine : BaseEntity
{
    public Guid TransferId { get; set; }
    public Guid ProductId { get; set; }
    public decimal QuantityRequested { get; set; }
    public decimal? QuantityReceived { get; set; }  // Set on confirmation; may differ if damaged in transit
    public string? Notes { get; set; }

    // Navigation
    public StockTransfer Transfer { get; set; } = null!;
    public Product Product { get; set; } = null!;
}
