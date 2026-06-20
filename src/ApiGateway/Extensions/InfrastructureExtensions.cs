using ApiGateway.ConfigurationSettings;
using ApiGateway.Plugins;
using System.ClientModel;

namespace ApiGateway.Extensions;

public static class InfrastructureExtensions
{
    public static IServiceCollection AddGatewayInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var redisSettings = services.AddAppSettings<RedisSettings>(config, "Redis");

        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var cfg = sp.GetRequiredService<RedisSettings>();
            var options = ConfigurationOptions.Parse(cfg.ConnectionString);

            // We set these to ensure that even when it eventually connects, it doesn't panic
            options.AbortOnConnectFail = false;
            options.ConnectRetry = 10;
            options.ReconnectRetryPolicy = new ExponentialRetry(5000); // Retry every 5s

            // By using a Lazy wrapper, the 'Connect' method isn't called
            // until the first time you call GetDatabase()
            var lazyConnection = new Lazy<ConnectionMultiplexer>(() =>
            {
                Console.WriteLine($"[Redis] Executing delayed connection to {cfg.ConnectionString}...");
                return ConnectionMultiplexer.Connect(options);
            });

            return lazyConnection.Value;
        });

        // 1. Register the underlying Plugin/Tool instances as Scoped
        // This allows the plugins themselves to use DI for DB contexts or HttpClient
        services.AddScoped<PharmacyNpiParser>();
        services.AddScoped<CardHolderIdParser>();

        // 2. Register the IChatClient Pipeline
        //services.AddChatClient(services =>
        //{
        //    // Configure the base Ollama/OpenAI connection
        //    var options = new OpenAIClientOptions { Endpoint = new Uri("http://localhost:11434/v1") };
        //    var baseClient = new OpenAI.Chat.ChatClient("llama3.2", new ApiKeyCredential("ollama"), options);

        //    return baseClient.AsIChatClient()
        //        .AsBuilder()
        //        .UseFunctionInvocation() // CRITICAL: This enables the agent to actually CALL your plugins
        //        .Build();
        //});

        // 3. Register the Agent itself
        services.AddScoped<ChatClientAgent>(sp =>
        {
            var chatClient = sp.GetRequiredService<IChatClient>();

            // Resolve plugins from DI and convert to Tools
            var npiPlugin = sp.GetRequiredService<PharmacyNpiParser>();
            var cardPlugin = sp.GetRequiredService<CardHolderIdParser>();

            return new ChatClientAgent(
                chatClient,
                name: "PharmacyParserAgent",
                instructions: "You are a PBM assistant. Use tools to extract NPIs and Member IDs. If a tool is not found to answer a question, use general PBM knowledge to answer questions, such as 'What is NDC?'",
                tools: new List<AITool>
                {
                    AIFunctionFactory.Create(npiPlugin.ExtractPharmacyNpi, name: "extract_pharmacy_npi"),
                    AIFunctionFactory.Create(cardPlugin.ExtractCardholderId, name: "extract_cardholder_id")
                });
        });

        return services;
    }
}