using System.Text.Json.Serialization;

namespace ApiGateway.ChatClients;

// Custom IChatClient wrapper handling the underlying API payload nuances
public partial class GeminiChatClient : IChatClient
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
    public async Task<ChatResponse> GetResponseAsync(
    IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
    ChatOptions? options = null,
    CancellationToken cancellationToken = default)
    {
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent";

        // Gemini handles system prompts via a dedicated field, NOT a user turn.
        // Pull them out instead of mapping System -> user.
        var systemText = string.Join(
            "\n",
            messages.Where(m => m.Role == ChatRole.System)
                    .Select(m => m.Text)
                    .Where(t => !string.IsNullOrEmpty(t)));

        var geminiContents = messages
            .Where(m => m.Role != ChatRole.System)
            .Select(m => new GeminiContent
            {
                Role = m.Role == ChatRole.Assistant ? "model" : "user",
                Parts = [new GeminiPart { Text = m.Text ?? string.Empty }]
            })
            .ToList();

        var requestBody = new GeminiRequest
        {
            Contents = geminiContents,
            SystemInstruction = string.IsNullOrEmpty(systemText)
                ? null
                : new GeminiContent { Parts = [new GeminiPart { Text = systemText }] }
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(requestBody)
        };
        httpRequest.Headers.Add("x-goog-api-key", _apiKey); // out of the query string

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            // Gemini puts the real reason in the body; EnsureSuccessStatusCode hides it.
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                $"Gemini request failed ({(int)response.StatusCode}) for model '{_model}': {error}");
        }

        var geminiResponse = await response.Content
            .ReadFromJsonAsync<GeminiResponse>(cancellationToken: cancellationToken);

        string replyText =
            geminiResponse?.Candidates is { Count: > 0 } candidates
            && candidates[0].Content?.Parts is { Count: > 0 } parts
                ? parts[0].Text ?? "No response generated."
                : "No response generated.";

        return new ChatResponse(
            new Microsoft.Extensions.AI.ChatMessage(ChatRole.Assistant, replyText));
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => serviceType.IsInstanceOfType(this) ? this : null;
    public void Dispose() => _httpClient.Dispose();


    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
