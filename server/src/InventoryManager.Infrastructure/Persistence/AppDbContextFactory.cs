using InventoryManager.Application.Common.Interfaces;
using InventoryManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace InventoryManager.Infrastructure.Persistence;

/// <summary>
/// Used ONLY by the EF Core CLI tools (dotnet ef migrations add / update).
/// Not used at runtime. Provides a design-time AppDbContext with a stub tenant service
/// so the migration tool can instantiate the context without an HTTP request.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(),
                "../InventoryManager.API"))  // Look for appsettings in the API project
            .AddJsonFile("appsettings.json")
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(config.GetConnectionString("DefaultConnection"))
            .Options;

        // Design-time stub — TenantId is irrelevant for migrations (schema is shared)
        return new AppDbContext(options, new DesignTimeTenantService());
    }

    /// <summary>
    /// Stub implementation that returns Guid.Empty for design-time use.
    /// The global query filters use this value, but no data is queried during migrations.
    /// </summary>
    private sealed class DesignTimeTenantService : ICurrentTenantService
    {
        public Guid TenantId => Guid.Empty;  // Safe for design-time — no actual queries run
        public void SetTenant(Guid tenantId) { }
    }
}
