using HexMaster.Battleship.Core.Cqrs;
using HexMaster.Battleship.Core.Observability;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.Extensions.DependencyInjection;

// Registers a handler as scoped and its interface as the instrumented decorator around it, so
// every use case is traced and measured without code in the handler (ADR 0008).
public static class CqrsRegistration
{
    public static IServiceCollection AddCommandHandler<TCommand, TResult, THandler>(this IServiceCollection services)
        where THandler : class, ICommandHandler<TCommand, TResult>
    {
        services.AddUseCaseInstrumentation();
        services.TryAddScoped<THandler>();
        services.AddScoped<ICommandHandler<TCommand, TResult>>(provider =>
            new InstrumentedCommandHandler<TCommand, TResult>(
                provider.GetRequiredService<THandler>(), provider.GetRequiredService<CoreMetrics>(), UseCaseLogger(provider)));

        return services;
    }

    public static IServiceCollection AddCommandHandler<TCommand, THandler>(this IServiceCollection services)
        where THandler : class, ICommandHandler<TCommand>
    {
        services.AddUseCaseInstrumentation();
        services.TryAddScoped<THandler>();
        services.AddScoped<ICommandHandler<TCommand>>(provider =>
            new InstrumentedCommandHandler<TCommand>(
                provider.GetRequiredService<THandler>(), provider.GetRequiredService<CoreMetrics>(), UseCaseLogger(provider)));

        return services;
    }

    public static IServiceCollection AddQueryHandler<TQuery, TResult, THandler>(this IServiceCollection services)
        where THandler : class, IQueryHandler<TQuery, TResult>
    {
        services.AddUseCaseInstrumentation();
        services.TryAddScoped<THandler>();
        services.AddScoped<IQueryHandler<TQuery, TResult>>(provider =>
            new InstrumentedQueryHandler<TQuery, TResult>(
                provider.GetRequiredService<THandler>(), provider.GetRequiredService<CoreMetrics>(), UseCaseLogger(provider)));

        return services;
    }

    private static void AddUseCaseInstrumentation(this IServiceCollection services)
    {
        services.AddMetrics();
        services.TryAddSingleton<CoreMetrics>();
    }

    private static ILogger UseCaseLogger(IServiceProvider provider) =>
        provider.GetService<ILoggerFactory>()?.CreateLogger(CoreTelemetry.UseCaseLoggerCategory) ?? NullLogger.Instance;
}
