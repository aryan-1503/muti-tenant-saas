using InventoryManager.Domain.Common;

namespace InventoryManager.Domain.Entities;

public class GoodsReceiptNoteLine : BaseEntity
{
    public Guid GoodsReceiptNoteId { get; set; }
    public Guid PurchaseOrderLineId { get; set; }
    public Guid ProductId { get; set; }
    public decimal QuantityReceived { get; set; }
    public string? Condition { get; set; }    // "Good", "Damaged", "Expired"
    public string? Notes { get; set; }

    // Navigation
    public GoodsReceiptNote GoodsReceiptNote { get; set; } = null!;
    public PurchaseOrderLine PurchaseOrderLine { get; set; } = null!;
    public Product Product { get; set; } = null!;
}
