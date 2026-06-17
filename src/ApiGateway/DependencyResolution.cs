using ApiGateway.ChatClients;
using ApiGateway.Extensions;

namespace ApiGateway;

public static class DependencyResolution
{
    public static IServiceCollection AddGeminiChatClient(this IServiceCollection services, IConfiguration config)
    {
        var settings = services.AddAppSettings<GeminiSettings>(config, "Gemini");
        services.AddTransient<IChatClient, GeminiChatClient>();
        return services;
    }
}