using System.Security.Claims;
using InventoryManager.Application.Common.Interfaces;
using InventoryManager.Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace InventoryManager.Infrastructure.Services;

/// <summary>
/// Reads the current authenticated user's identity from the HttpContext.
/// Claims are set by JwtBearer middleware after validating the access token.
/// This service is injected into command handlers to get CreatedByUserId etc.
/// </summary>
public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? Principal => _httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public Guid UserId
    {
        get
        {
            var sub = Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                   ?? Principal?.FindFirst("sub")?.Value;
            return Guid.TryParse(sub, out var id) ? id : Guid.Empty;
        }
    }

    public Guid TenantId
    {
        get
        {
            var claim = Principal?.FindFirst("tenantId")?.Value;
            return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
        }
    }

    public string Email => Principal?.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty;

    public UserRole Role
    {
        get
        {
            var role = Principal?.FindFirst(ClaimTypes.Role)?.Value;
            return Enum.TryParse<UserRole>(role, out var r) ? r : UserRole.ReadOnly;
        }
    }
}
