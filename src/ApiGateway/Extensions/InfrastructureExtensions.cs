using ApiGateway.ConfigurationSettings;

namespace ApiGateway.Extensions;

public static class InfrastructureExtensions
{
    public static IServiceCollection AddGatewayInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        //services.AddAppSettings<RedisSettings>(config, "Redis");

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

        services.AddTransient(sp =>
        {
            var kernelBuilder = Kernel.CreateBuilder();
            // Configure based on scoped data, e.g., user preferences
            kernelBuilder.AddOpenAIChatCompletion(
                modelId: "llama3.2",
                apiKey: "anything", // API key is ignored by Ollama
                endpoint: new Uri("http://localhost:11434/v1"));

            kernelBuilder.Services.AddSingleton(sp.GetRequiredService<ILoggerFactory>().CreateLogger("Kernel"));
            kernelBuilder.Services.AddSingleton(sp.GetRequiredService<IConfiguration>());

            return kernelBuilder.Build();
        });

        return services;
    }
}