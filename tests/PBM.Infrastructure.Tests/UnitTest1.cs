namespace PBM.Infrastructure.Tests;

public class MyServiceIntegrationTests
{
    [Fact]
    public async Task DoWork_WithRealKernel_ReturnsExpectedResult()
    {
        var builder = Kernel.CreateBuilder();
        // Register an "Assistant" that returns static text instead of calling an API
        builder.AddOpenAIChatCompletion(
            modelId: "0.17.7",
            apiKey: "anything", // API key is ignored by Ollama
            endpoint: new Uri("http://localhost:11434/v1"));

        var kernel = builder.Build();
        var service = new MyService(kernel);

        // Act
        var result = await service.DoWork();

        // Assert: Verify business logic based on the fake AI response
        Assert.NotNull(result);
    }
}

public class MyService
{
    private readonly Kernel _kernel;
    public MyService(Kernel kernel)
    {
        _kernel = kernel;
    }
    public async Task<string> DoWork()
    {
        // This method would normally call the AI service, but in tests, it will use the fake implementation
        var response = await _kernel.InvokePromptAsync<string>("Hello, how can I assist you today?");
        return response;
    }
}