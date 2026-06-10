using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace InventoryManager.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // FluentValidation — auto-register all validators in the Application assembly
        services.AddValidatorsFromAssembly(typeof(AssemblyReference).Assembly);

        return services;
    }
}
