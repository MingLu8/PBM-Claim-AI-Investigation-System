namespace ApiGateway.ChatClients;

public class OllamaSettings
{
    public string Endpoint { get; init; } = "http://localhost:11434";
    public string Model { get; init; } = "llama4";
}
