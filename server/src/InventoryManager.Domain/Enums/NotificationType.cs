namespace InventoryManager.Domain.Enums;

public enum NotificationType
{
    LowStock = 1,          // Item has fallen at or below its reorder point
    PODeliveryDue = 2,     // A PO's expected delivery date is today or overdue
    GRNDiscrepancy = 3,    // A GRN was submitted with unresolved discrepancies
    StockCountDue = 4,     // Scheduled reminder to perform a stock count
    TransferPending = 5    // A transfer request is awaiting action at a location
}
