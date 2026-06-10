namespace InventoryManager.Domain.Enums;

public enum StockCountStatus
{
    Created = 1,    // Session created, not yet assigned or started
    InProgress = 2, // Staff are actively counting
    Submitted = 3,  // Staff submitted their counts — variances calculated
    Reviewing = 4,  // Manager is reviewing variances
    Completed = 5   // All variances resolved, adjustments posted
}
