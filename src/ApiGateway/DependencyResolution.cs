using ApiGateway.ChatClients;
using ApiGateway.ConfigurationSettings;
using ApiGateway.Extensions;
using ApiGateway.Plugins;
using ApiGateway.Services;
using Microsoft.Extensions.AI;

namespace ApiGateway;

public static class DependencyResolution
{
    public static IServiceCollection AddApplicationDependencies(this IServiceCollection services, IConfiguration config)
    {
        services.AddHttpClient();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();

        var redisSettings = services.AddAppSettings<RedisSettings>(config, "Redis");
        var memorySettings = services.AddAppSettings<MemoryOptions>(config, "memory");
        var geminiSettings = services.AddAppSettings<GeminiSettings>(config, "Gemini");
        var ollamaSettings = services.AddAppSettings<OllamaSettings>(config, "Ollama");
        var agentSettings = services.AddAppSettings<AgentSettings>(config, "Agent");

        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var options = ConfigurationOptions.Parse(redisSettings.ConnectionString);

            // We set these to ensure that even when it eventually connects, it doesn't panic
            options.AbortOnConnectFail = false;
            options.ConnectRetry = 10;
            options.ReconnectRetryPolicy = new ExponentialRetry(5000); // Retry every 5s

            // By using a Lazy wrapper, the 'Connect' method isn't called
            // until the first time you call GetDatabase()
            var lazyConnection = new Lazy<ConnectionMultiplexer>(() =>
            {
                Console.WriteLine($"[Redis] Executing delayed connection to {redisSettings.ConnectionString}...");
                return ConnectionMultiplexer.Connect(options);
            });

            return lazyConnection.Value;
        });

        if (!string.IsNullOrWhiteSpace(redisSettings.ConnectionString))
            services.AddSingleton<ISessionStore, RedisSessionStore>();
        else
            services.AddSingleton<ISessionStore, InMemorySessionStore>();

        services.AddScoped<IEmbedder, GeminiEmbedder>();
        services.AddScoped<IRagSearch, InMemoryRagSearch>();
        services.AddScoped<PharmacyNpiParser>();
        services.AddScoped<CardHolderIdParser>();
        services.AddScoped<RecallMemoryTool>();

        services.AddTransient<GeminiChatClient>();
        services.AddTransient<IChatClient>(sp =>
        {
            var primary = sp.GetRequiredService<GeminiChatClient>();
            var ollama = new OllamaChatClient(
                new Uri(ollamaSettings.Endpoint),
                modelId: ollamaSettings.Model);
            return new FallbackChatClient(
                primary,
                ollama,
                sp.GetRequiredService<ILogger<FallbackChatClient>>(),
                primaryName: "Gemini",
                fallbackName: $"Ollama ({ollamaSettings.Model})");
        });
        // Builds a ChatClientAgent per request for the caller's chosen LLM provider
        // ("auto" = Gemini→Ollama fallback, "gemini", or "ollama"). See ChatAgentFactory.
        services.AddScoped<IChatAgentFactory, ChatAgentFactory>();

        var embeddingDims = config.GetValue<int?>("Embeddings:Dimensions") ?? 256;
        switch (config["Embeddings:Provider"]?.Trim().ToLowerInvariant())
        {
            case "inmemory":
                services.AddSingleton<IEmbedder>(_ => new InMemoryEmbedder(embeddingDims));
                break;
            case "azure":
                services.AddSingleton<AzureOpenAIEmbedder>();
                services.AddSingleton<IEmbedder>(sp => new FallbackEmbedder(
                    sp.GetRequiredService<AzureOpenAIEmbedder>(),
                    new InMemoryEmbedder(embeddingDims),
                    sp.GetRequiredService<ILogger<FallbackEmbedder>>()));
                break;
            default: // "gemini"
                services.AddSingleton<GeminiEmbedder>();
                services.AddSingleton<IEmbedder>(sp => new FallbackEmbedder(
                    sp.GetRequiredService<GeminiEmbedder>(),
                    new InMemoryEmbedder(embeddingDims),
                    sp.GetRequiredService<ILogger<FallbackEmbedder>>()));
                break;
        }
        return services;
    }
}