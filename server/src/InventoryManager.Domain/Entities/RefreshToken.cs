using InventoryManager.Domain.Common;

namespace InventoryManager.Domain.Entities;

/// <summary>
/// Stores refresh tokens for JWT rotation.
/// Each login or refresh creates a new token. The old one is revoked.
/// Stored in DB (not Redis) so we can audit and revoke by user/device.
/// </summary>
public class RefreshToken : TenantScopedEntity
{
    public Guid UserId { get; set; }
    public string Token { get; set; } = string.Empty;  // Opaque secure random string
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? ReplacedByToken { get; set; }  // Set when rotated — allows tracking rotation chain

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsRevoked => RevokedAt.HasValue;
    public bool IsActive => !IsRevoked && !IsExpired;

    // Navigation
    public AppUser User { get; set; } = null!;
}
