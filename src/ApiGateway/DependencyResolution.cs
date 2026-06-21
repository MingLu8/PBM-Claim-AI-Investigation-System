using ApiGateway.ChatClients;
using ApiGateway.Extensions;
using ApiGateway.Services;

namespace ApiGateway;

public static class DependencyResolution
{
    public static IServiceCollection AddGeminiChatClient(this IServiceCollection services, IConfiguration config)
    {
        services.AddHttpClient();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();

        var settings = services.AddAppSettings<GeminiSettings>(config, "Gemini");
        services.AddTransient<IChatClient, GeminiChatClient>();

        var redisConn = config["Redis:ConnectionString"];
        if (!string.IsNullOrWhiteSpace(redisConn))
        {
            services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisConn));
            services.AddSingleton<ISessionStore, RedisSessionStore>();
        }
        else
            services.AddSingleton<ISessionStore, InMemorySessionStore>();

        return services;
    }
}