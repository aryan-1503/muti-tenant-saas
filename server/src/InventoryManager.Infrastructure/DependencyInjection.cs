using InventoryManager.Application.Common.Interfaces;
using InventoryManager.Infrastructure.Persistence;
using InventoryManager.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace InventoryManager.Infrastructure;

/// <summary>
/// Extension method to register all Infrastructure services in one call from Program.cs.
/// This keeps Program.cs clean and ensures Infrastructure is properly wired.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ─── Database ─────────────────────────────────────────────────────────
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                b => b.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)));

        // ─── Redis ────────────────────────────────────────────────────────────
        // Used for: invite tokens, password reset tokens, low-stock alert deduplication
        services.AddSingleton<IConnectionMultiplexer>(sp =>
            ConnectionMultiplexer.Connect(configuration.GetConnectionString("Redis")!));

        // ─── Core Services ────────────────────────────────────────────────────
        // Scoped = one instance per HTTP request. This is critical for CurrentTenantService
        // because the tenant changes between requests.
        services.AddScoped<ICurrentTenantService, CurrentTenantService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IEmailService, EmailService>();

        // Required by CurrentUserService to read HttpContext claims
        services.AddHttpContextAccessor();

        return services;
    }
}
