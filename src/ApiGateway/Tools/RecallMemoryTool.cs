using ApiGateway.ChatClients;
using ApiGateway.Services;
using System.Text;
using System.Text.Json;

namespace ApiGateway.Plugins;

public sealed class MemoryOptions
{
    public bool Enabled { get; set; }
    public int TopK { get; set; }
    public double MinScore { get; set; } = 0.20;
}
public record SourceRef(string Title, string Excerpt, string Url, double Score);
public record ToolResult(string Json, IEnumerable<SourceRef>? Sources = null);
public interface IAgentTool
{
    string Name { get; }
    bool IsEnabled { get; }

    ChatTool Definition { get; }

    Task<ToolResult> InvokeAsync(JsonElement jsonElement, CancellationToken cancellationToken);
}

public record RagHit(string Text, double Score);
public interface IRagSearch
{
    bool IsEnabled { get; }
    Task<IEnumerable<RagHit>> SearchAsync(string query, IEnumerable<string> documents, int topK, double minScore, CancellationToken cancellationToken);
}
public interface IEmbedder
{
    bool IsEnabled { get; }
    Task<float[]?> EmbedAsync(string text, CancellationToken cancellationToken);
}

public class AzureOpenAIOptions
{
    public string? Endpoint { get; set; }
    public string? ApiKey { get; set; }

    public string? Deployment { get; set; } = "gpt-4o";
    public string? EmbeddingDeployment { get; set; }

}

public sealed class AzureOpenAIEmbedder : IEmbedder
{
    public AzureOpenAIEmbedder(AzureOpenAIOptions options, ILogger<AzureOpenAIEmbedder> logger)
    {

    }
    public bool IsEnabled => throw new NotImplementedException();

    public Task<float[]?> EmbedAsync(string text, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}

public class GeminiEmbedder : IEmbedder
{
    private readonly GeminiSettings _geminiSesttings;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GeminiEmbedder> _logger;

    public GeminiEmbedder(
        GeminiSettings geminiSesttings,
        IHttpClientFactory httpClientFactory,
        ILogger<GeminiEmbedder> logger)
    {
        _geminiSesttings = geminiSesttings;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public bool IsEnabled => !string.IsNullOrWhiteSpace(_geminiSesttings.ApiKey);

    public async Task<float[]?> EmbedAsync(string text, CancellationToken cancellationToken)
    {
        if (!IsEnabled || string.IsNullOrWhiteSpace(text)) return null;

        var http = _httpClientFactory.CreateClient();
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_geminiSesttings.EmbeddingModel}:embedContent?key={_geminiSesttings.ApiKey}";
        var paylaod = new
        {
            model = $"models/{_geminiSesttings.EmbeddingModel}",
            content = new { parts = new[] { new { text } } }
        };

        using var body = new StringContent(JsonSerializer.Serialize(paylaod), Encoding.UTF8, "application/json");
        using var res = await http.PostAsync(url, body, cancellationToken);
        if (!res.IsSuccessStatusCode)
        {
            _logger.LogWarning("[Gemini] embedContent failed ({Status})", (int)res.StatusCode);
            return null;
        }

        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(cancellationToken));
        var values = doc.RootElement.GetProperty("embedding").GetProperty("values");
        var arr = new float[values.GetArrayLength()];
        var i = 0;
        foreach (var v in values.EnumerateArray()) arr[i++] = v.GetSingle();
        return arr;
    }
}

public class InMemoryRagSearch(IEmbedder embedder) : IRagSearch
{
    public bool IsEnabled => throw new NotImplementedException();

    public Task<IEnumerable<RagHit>> SearchAsync(string query, IEnumerable<string> documents, int topK, double minScore, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
public class RecallMemoryTool : IAgentTool
{
    private readonly ISessionStore _store;
    private readonly ICurrentUser _currentUser;
    private readonly IRagSearch _rag;
    private readonly MemoryOptions _options;
    private readonly ILogger<RecallMemoryTool> _logger;

    public RecallMemoryTool(
        ISessionStore store,
        ICurrentUser currentUser,
        IRagSearch rag,
        MemoryOptions options,
        ILogger<RecallMemoryTool> logger
        )
    {
        _store = store;
        _currentUser = currentUser;
        _rag = rag;
        _options = options;
        _logger = logger;
    }
    public string Name => "recall_past_conversations";

    public bool IsEnabled => _options.Enabled;

    public ChatTool Definition => ChatTool.CreateFunctionTool(
        Name,
        @"Search THIS user's earlier conversations for relevant facts. Call this when the user reference a 
          previous chat, uses an entity or pronoun with no antecedent in the current conversation, or asks 
          to contine earlier work. Returns the most relevant snippets (my be empty).",
        BinaryData.FromString("""
            {"type": "object", "properties": {"query": {"type":"string", "description":"What to recall."}}, "required":["query"]}
            """));

    public async Task<ToolResult> InvokeAsync(JsonElement jsonElement, CancellationToken cancellationToken)
    {
        var query = jsonElement.GetString("query") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(query))
            return new ToolResult("{\"snippets\": []}");

        var sessions = await _store.GetAllAsync(_currentUser.UserId);
        var turns = sessions.SelectMany(a => a.History).Select(a => $"{a.Role}: {a.Content}")
            .Distinct()
            .ToList();

        if (!turns.Any())
            return new ToolResult("{\"snippets\": []}");

        if (_rag.IsEnabled)
        {
            var hits = await _rag.SearchAsync(query, turns, _options.TopK, _options.MinScore, cancellationToken);
            var snippets = hits.Select(a => a.Text).ToList();
            _logger.LogInformation("[Memory] RAG recall: {Count} hit(s) for '{Query}'", snippets.Count, query);
            return new ToolResult(JsonSerializer.Serialize(new { method = "rag", count = snippets.Count, snippets }));
        }

        var terms = query.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var keywordHits = turns.Where(a => terms.Any(b => a.ToLowerInvariant().Contains(b)))
                                .Take(_options.TopK).ToList();

        return new ToolResult(JsonSerializer.Serialize(new { method = "keyword", count = keywordHits.Count, snippets = keywordHits }));
    }
}

public static class ToolJson
{
    public static string? GetString(this JsonElement el, string prop)
    {
        return el.ValueKind == JsonValueKind.Object && el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;
    }
}
