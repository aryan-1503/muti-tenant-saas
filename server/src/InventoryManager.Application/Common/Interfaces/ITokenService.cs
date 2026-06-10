namespace InventoryManager.Application.Common.Interfaces;

public interface ITokenService
{
    /// <summary>Generates a short-lived JWT access token (15 min).</summary>
    string GenerateAccessToken(Guid userId, Guid tenantId, string email, string role);

    /// <summary>Generates a secure random refresh token string.</summary>
    string GenerateRefreshToken();

    /// <summary>Extracts the userId from an (expired) access token without throwing.</summary>
    Guid? GetUserIdFromToken(string token);
}
