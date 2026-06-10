namespace InventoryManager.Domain.Enums;

public enum TransferStatus
{
    Pending = 1,    // Created but source hasn't dispatched yet
    InTransit = 2,  // Dispatched from source — stock deducted, not yet at destination
    Completed = 3,  // Destination confirmed receipt — stock added
    Cancelled = 4   // Voided before dispatch
}
