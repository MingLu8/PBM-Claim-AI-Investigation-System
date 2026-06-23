namespace ApiGateway.ChatClients
{
    public class GeminiSettings
    {
        public string? ApiKey { get; init; }
        public string? Model { get; init; }
        public object EmbeddingModel { get; internal set; }
    }
}
