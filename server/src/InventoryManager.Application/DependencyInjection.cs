using FluentValidation;
using InventoryManager.Application.Common.Behaviours;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace InventoryManager.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // FluentValidation — auto-register all validators in the Application assembly
        services.AddValidatorsFromAssembly(typeof(AssemblyReference).Assembly);

        // Register MediatR pipeline: ValidationBehaviour runs before every handler
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehaviour<,>));

        return services;
    }
}

