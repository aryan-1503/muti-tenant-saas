namespace InventoryManager.Domain.Enums;

/// <summary>
/// Every single stock change must have a type.
/// This drives the audit trail and reporting logic.
/// </summary>
public enum MovementType
{
    OpeningBalance = 1,    // Initial stock seeding when the business first sets up
    GoodsIn = 2,           // Stock received via a GRN (from a PO)
    ManualAdjustment = 3,  // Admin correction — always requires a reason
    TransferOut = 4,       // Leaving a warehouse in a transfer
    TransferIn = 5,        // Arriving at a warehouse from a transfer
    CountAdjustment = 6,   // Variance posted after a stock count review
    SaleDeduction = 7,     // Future: auto-deduct on sale (POS/e-commerce integration)
    WriteOff = 8           // Damaged/expired stock removed from inventory
}
