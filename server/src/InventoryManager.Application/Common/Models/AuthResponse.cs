namespace InventoryManager.Application.Common.Models;

/// <summary>
/// Returned by every auth endpoint (register, login, refresh).
/// The access token goes in the response body.
/// The refresh token is extracted by the controller and set as an HttpOnly cookie —
/// it is NOT serialized to the client in the JSON body.
/// </summary>
public record AuthResponse(
    string AccessToken,
    DateTime AccessTokenExpiry,
    UserDto User
)
{
    /// <summary>
    /// Set by the handler; read by the controller to set the HttpOnly cookie.
    /// Excluded from JSON serialization via [JsonIgnore] in the controller mapping.
    /// </summary>
    public string RefreshToken { get; init; } = string.Empty;
}

public record UserDto(
    Guid Id,
    string FullName,
    string Email,
    string Role,
    Guid TenantId,
    string TenantName
);

