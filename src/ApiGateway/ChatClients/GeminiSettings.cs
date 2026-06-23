namespace ApiGateway.ChatClients
{
    public class GeminiSettings
    {
        public string? ApiKey { get; init; }
        public string? Model { get; init; }

        // Model used for the embedContent endpoint that powers semantic memory recall.
        public string EmbeddingModel { get; init; } = "text-embedding-004";
    }
}
