namespace InventoryManager.Application;

/// <summary>
/// Marker class — used by MediatR and FluentValidation to discover
/// all handlers and validators in the Application assembly via reflection.
///
/// Usage in Program.cs:
///   services.AddMediatR(cfg =>
///       cfg.RegisterServicesFromAssembly(typeof(AssemblyReference).Assembly));
/// </summary>
public sealed class AssemblyReference { }
