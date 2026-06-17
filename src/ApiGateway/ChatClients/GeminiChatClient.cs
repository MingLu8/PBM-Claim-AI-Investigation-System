using System.Text.Json.Serialization;

namespace ApiGateway.ChatClients;

// Custom IChatClient wrapper handling the underlying API payload nuances
public class GeminiChatClient : IChatClient
{
    private readonly HttpClient _httpClient = new();
    private readonly string _apiKey;
    private readonly string _model;

    public GeminiChatClient(GeminiSettings settings)
    {
        _apiKey = settings.ApiKey;
        _model = settings.Model;
        Metadata = new ChatClientMetadata("GoogleGemini", new Uri("https://generativelanguage.googleapis.com"), _model);
    }

    public ChatClientMetadata Metadata { get; }
    public async Task<ChatResponse> GetResponseAsync(IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {

        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent?key={_apiKey}";

        // Critical conversion step: Gemini strictly expects "user" or "model" roles.
        // Microsoft Agent Framework defaults to System/User/Assistant.
        var geminiContents = new List<GeminiContent>();
        foreach (var msg in messages)
        {
            string mappedRole = msg.Role == ChatRole.Assistant ? "model" : "user";
            geminiContents.Add(new GeminiContent
            {
                Role = mappedRole,
                Parts = [new GeminiPart { Text = msg.Text ?? string.Empty }]
            });
        }

        var requestBody = new GeminiRequest { Contents = geminiContents };
        var response = await _httpClient.PostAsJsonAsync(url, requestBody, cancellationToken);
        response.EnsureSuccessStatusCode();

        var geminiResponse = await response.Content.ReadFromJsonAsync<GeminiResponse>(cancellationToken: cancellationToken);
        string replyText = geminiResponse?.Candidates?[0].Content?.Parts?[0].Text ?? "No response generated.";

        return new ChatResponse(new Microsoft.Extensions.AI.ChatMessage(ChatRole.Assistant, replyText));
    }


    public object? GetService(Type serviceType, object? serviceKey = null) => serviceType.IsInstanceOfType(this) ? this : null;
    public void Dispose() => _httpClient.Dispose();


    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    // Internal payload representations for Google Gemini REST mapping
    private class GeminiRequest { [JsonPropertyName("contents")] public List<GeminiContent> Contents { get; set; } = []; }
    private class GeminiContent { [JsonPropertyName("role")] public string Role { get; set; } = string.Empty; [JsonPropertyName("parts")] public List<GeminiPart> Parts { get; set; } = []; }
    private class GeminiPart { [JsonPropertyName("text")] public string Text { get; set; } = string.Empty; }
    private class GeminiResponse { [JsonPropertyName("candidates")] public List<GeminiCandidate>? Candidates { get; set; } }
    private class GeminiCandidate { [JsonPropertyName("content")] public GeminiContent? Content { get; set; } }
}
