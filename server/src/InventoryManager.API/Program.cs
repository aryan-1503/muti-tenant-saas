using System.Text;
using InventoryManager.API.Middleware;
using InventoryManager.Application;
using InventoryManager.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Serilog;

// ─── Serilog bootstrap logger ────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // ─── Serilog (full) ───────────────────────────────────────────────────────
    builder.Host.UseSerilog((ctx, lc) => lc
        .ReadFrom.Configuration(ctx.Configuration)
        .WriteTo.Console(outputTemplate:
            "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {NewLine}{Exception}"));

    // ─── Infrastructure (DB, Redis, Services) ────────────────────────────────
    builder.Services.AddInfrastructure(builder.Configuration);

    // ─── Application (MediatR + FluentValidation) ────────────────────────────
    builder.Services.AddMediatR(cfg =>
        cfg.RegisterServicesFromAssembly(typeof(InventoryManager.Application.AssemblyReference).Assembly));
    builder.Services.AddApplication();  // Registers all FluentValidation validators

    // ─── Controllers ─────────────────────────────────────────────────────────
    builder.Services.AddControllers();
    builder.Services.AddOpenApi();

    // ─── CORS (for Angular dev server) ───────────────────────────────────────
    builder.Services.AddCors(options => options.AddPolicy("Angular", policy =>
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()));   // AllowCredentials needed for HttpOnly cookie refresh tokens

    // ─── JWT Authentication ───────────────────────────────────────────────────
    var jwtKey = builder.Configuration["Jwt:Key"]
        ?? throw new InvalidOperationException("JWT Key is not configured");

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = builder.Configuration["Jwt:Issuer"],
                ValidAudience = builder.Configuration["Jwt:Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                ClockSkew = TimeSpan.Zero  // No grace period — tokens expire exactly at ExpiresAt
            };
        });

    builder.Services.AddAuthorization();

    // ─── Build ────────────────────────────────────────────────────────────────
    var app = builder.Build();

    // ─── Pipeline ─────────────────────────────────────────────────────────────
    // ORDER MATTERS. Middleware runs top-to-bottom on request, bottom-to-top on response.

    app.UseMiddleware<GlobalExceptionMiddleware>();  // Must be first — catches all exceptions

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }

    app.UseSerilogRequestLogging();

    app.UseHttpsRedirection();

    app.UseCors("Angular");

    app.UseAuthentication();   // Validates JWT, sets HttpContext.User
    app.UseAuthorization();    // Enforces [Authorize] attributes

    app.UseMiddleware<TenantResolutionMiddleware>();  // After auth — reads tenantId claim

    app.MapControllers();

    // ─── Auto-migrate on startup (dev convenience) ───────────────────────────
    if (app.Environment.IsDevelopment())
    {
        using var scope = app.Services.CreateScope();
        // Migrations run automatically in development. In production, use CI/CD migration steps.
        // var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        // await db.Database.MigrateAsync();
    }

    Log.Information("InventoryManager API started on {Urls}", string.Join(", ", app.Urls));
    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
