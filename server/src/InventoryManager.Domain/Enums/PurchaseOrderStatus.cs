namespace InventoryManager.Domain.Enums;

public enum PurchaseOrderStatus
{
    Draft = 1,             // Being built, not yet sent to supplier
    Sent = 2,              // Sent to supplier, awaiting confirmation
    Confirmed = 3,         // Supplier confirmed they will fulfil
    PartiallyReceived = 4, // Some goods arrived, some still outstanding
    FullyReceived = 5,     // All line items received
    Closed = 6,            // Manually closed (e.g. supplier can't fulfil remainder)
    Cancelled = 7          // Voided before any receiving
}
