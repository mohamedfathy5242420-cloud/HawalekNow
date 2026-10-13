using System.Reflection;
using FluentValidation;
using HawalekNow.Application.Shared.Behaviors;
using HawalekNow.Application.Shared.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HawalekNow.Application.Shared;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, params Assembly[] additionalAssemblies)
    {
        var assemblies = additionalAssemblies.Prepend(typeof(DependencyInjection).Assembly).Distinct().ToArray();
        services.TryAddScoped<CorrelationContext>();
        services.AddMediatR(configuration =>
        {
            configuration.RegisterServicesFromAssemblies(assemblies);
            // First registered is outermost in MediatR 12.5.0.
            configuration.AddOpenBehavior(typeof(LoggingBehavior<,>));
            configuration.AddOpenBehavior(typeof(PerformanceBehavior<,>));
            configuration.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });
        services.AddValidatorsFromAssemblies(assemblies, ServiceLifetime.Transient);
        return services;
    }
}
