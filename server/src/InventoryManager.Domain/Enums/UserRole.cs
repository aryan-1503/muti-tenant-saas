namespace InventoryManager.Domain.Enums;

public enum UserRole
{
    Admin = 1,    // Full access — org owner
    Manager = 2,  // Can do everything except user management
    Staff = 3,    // Operational tasks only (GRN, counts, transfers)
    ReadOnly = 4  // View only
}
