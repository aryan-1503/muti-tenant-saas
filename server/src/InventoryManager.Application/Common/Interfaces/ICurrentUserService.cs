using InventoryManager.Domain.Enums;

namespace InventoryManager.Application.Common.Interfaces;

/// <summary>
/// Provides the currently authenticated user's identity.
/// Used by command handlers to set CreatedByUserId on new records
/// and enforce role-based access within slices.
/// </summary>
public interface ICurrentUserService
{
    Guid UserId { get; }
    Guid TenantId { get; }
    string Email { get; }
    UserRole Role { get; }
    bool IsAuthenticated { get; }
}
