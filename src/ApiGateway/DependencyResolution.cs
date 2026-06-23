using ApiGateway.ChatClients;
using ApiGateway.ConfigurationSettings;
using ApiGateway.Extensions;
using ApiGateway.Plugins;
using ApiGateway.Services;

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

        services.AddTransient<IChatClient, GeminiChatClient>();
        services.AddScoped(sp =>
        {
            var chatClient = sp.GetRequiredService<IChatClient>();

            // Resolve plugins from DI and convert to Tools
            var npiPlugin = sp.GetRequiredService<PharmacyNpiParser>();
            var cardPlugin = sp.GetRequiredService<CardHolderIdParser>();
            var memoryTool = sp.GetRequiredService<RecallMemoryTool>();

            return new ChatClientAgent(
                chatClient,
                name: "PharmacyParserAgent",
                instructions: $"You are a PBM assistant. You have memory use {memoryTool.Definition.FunctionName} tool to retrieve facts, Use tools to extract NPIs and Member IDs. If a tool is not found to answer a question, use general PBM knowledge to answer questions, such as 'What is NDC?'",
                tools: new List<AITool>
                {
                    AIFunctionFactory.Create(npiPlugin.ExtractPharmacyNpi, name: "extract_pharmacy_npi"),
                    AIFunctionFactory.Create(cardPlugin.ExtractCardholderId, name: "extract_cardholder_id"),
                    AIFunctionFactory.Create(memoryTool.RecallAsync, name: memoryTool.Definition.FunctionName, description: memoryTool.Definition.FunctionDescription)
                });
        });

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