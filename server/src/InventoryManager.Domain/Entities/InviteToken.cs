using InventoryManager.Domain.Common;
using InventoryManager.Domain.Enums;

namespace InventoryManager.Domain.Entities;

/// <summary>
/// A pending invitation for someone to join a tenant.
/// Token is short-lived (48h) and stored with an expiry.
/// Once accepted, AcceptedAt is set and the token is considered used.
/// </summary>
public class InviteToken : TenantScopedEntity
{
    public string Email { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Staff;
    public string Token { get; set; } = string.Empty;  // Secure random token (32 bytes, base64url)
    public DateTime ExpiresAt { get; set; }
    public DateTime? AcceptedAt { get; set; }

    public bool IsExpired => DateTime.UtcNow > ExpiresAt;
    public bool IsUsed => AcceptedAt.HasValue;
    public bool IsValid => !IsExpired && !IsUsed;

    // Navigation
    public Tenant Tenant { get; set; } = null!;
}
