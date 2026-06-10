using InventoryManager.Domain.Common;

namespace InventoryManager.Domain.Entities;

public class PurchaseOrderLine : BaseEntity
{
    public Guid PurchaseOrderId { get; set; }
    public Guid ProductId { get; set; }
    public decimal QuantityOrdered { get; set; }
    public decimal QuantityReceived { get; set; }   // Accumulated across all GRNs for this PO
    public decimal UnitCost { get; set; }           // Price per unit at time of order

    // Navigation
    public PurchaseOrder PurchaseOrder { get; set; } = null!;
    public Product Product { get; set; } = null!;
    public ICollection<GoodsReceiptNoteLine> GRNLines { get; set; } = [];
}
