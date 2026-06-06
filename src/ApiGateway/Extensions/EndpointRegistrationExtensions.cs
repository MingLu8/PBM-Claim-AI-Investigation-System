using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace ApiGateway.Endpoints;

public static class EndpointRegistrationExtensions
{
    // 1. Scan and add the endpoints to the DI Container
    public static IServiceCollection AddEndpoints(this IServiceCollection services, Assembly assembly)
    {
        var endpointTypes = assembly.GetTypes()
            .Where(t => typeof(IEndpoint).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

        foreach (var type in endpointTypes)
        {
            // Register as Transient so they are resolved cleanly on startup
            services.AddTransient(typeof(IEndpoint), type);
        }

        return services;
    }

    // 2. Resolve them from DI and map them to the application pipeline
    public static WebApplication MapEndpoints(this WebApplication app, RouteGroupBuilder? routeGroupBuilder = null)
    {
        // 1. Create a temporary scope to satisfy scoped constructor dependencies
        using var scope = app.Services.CreateScope();

        // 2. Resolve the endpoints from the temporary scope's provider
        var endpoints = scope.ServiceProvider.GetRequiredService<IEnumerable<IEndpoint>>();

        IEndpointRouteBuilder builder = (IEndpointRouteBuilder?)routeGroupBuilder ?? app;

        foreach (var endpoint in endpoints)
        {
            endpoint.MapEndpoint(builder);
        }

        return app;
    }
}