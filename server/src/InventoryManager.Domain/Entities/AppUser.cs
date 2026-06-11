using InventoryManager.Domain.Common;
using InventoryManager.Domain.Enums;

namespace InventoryManager.Domain.Entities;

/// <summary>
/// A user who belongs to one Tenant. Users are NOT shared across tenants.
/// Role determines what they can do within their tenant.
/// </summary>
public class AppUser : TenantScopedEntity
{
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Staff;
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }
    public string? PasswordResetToken { get; set; }
    public DateTime? PasswordResetTokenExpiresAt { get; set; }

    // Navigation
    public Tenant Tenant { get; set; } = null!;
}
