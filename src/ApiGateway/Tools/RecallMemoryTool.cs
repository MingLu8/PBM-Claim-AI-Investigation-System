using ApiGateway.ChatClients;
using ApiGateway.Services;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Text;
using System.Text.Json;

namespace ApiGateway.Plugins;

public sealed class MemoryOptions
{
    // Enabled by default so cross-session recall works out of the box; override via the "memory" config section.
    public bool Enabled { get; set; } = true;
    public int TopK { get; set; } = 5;
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
    // Embeddings for a given piece of text are stable, so cache them to avoid re-embedding
    // the same past turns on every recall. (Static for the process; fine for this workload.)
    private static readonly ConcurrentDictionary<string, float[]> _embeddingCache = new();

    public bool IsEnabled => embedder.IsEnabled;

    public async Task<IEnumerable<RagHit>> SearchAsync(string query, IEnumerable<string> documents, int topK, double minScore, CancellationToken cancellationToken)
    {
        if (!IsEnabled) return Array.Empty<RagHit>();

        var docs = documents as IList<string> ?? documents.ToList();
        if (docs.Count == 0) return Array.Empty<RagHit>();
        if (topK <= 0) topK = 5;

        var queryVector = await embedder.EmbedAsync(query, cancellationToken);
        if (queryVector is null) return Array.Empty<RagHit>();

        var hits = new List<RagHit>(docs.Count);
        foreach (var doc in docs)
        {
            var docVector = await EmbedCachedAsync(doc, cancellationToken);
            if (docVector is null) continue;

            var score = CosineSimilarity(queryVector, docVector);
            if (score >= minScore)
                hits.Add(new RagHit(doc, score));
        }

        return hits.OrderByDescending(h => h.Score).Take(topK).ToList();
    }

    private async Task<float[]?> EmbedCachedAsync(string text, CancellationToken cancellationToken)
    {
        if (_embeddingCache.TryGetValue(text, out var cached))
            return cached;

        var vector = await embedder.EmbedAsync(text, cancellationToken);
        if (vector is not null)
            _embeddingCache[text] = vector;

        return vector;
    }

    private static double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length || a.Length == 0) return 0;

        double dot = 0, magA = 0, magB = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            magA += a[i] * a[i];
            magB += b[i] * b[i];
        }

        if (magA == 0 || magB == 0) return 0;
        return dot / (Math.Sqrt(magA) * Math.Sqrt(magB));
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

    // IAgentTool entry point (JSON in / JSON out).
    public async Task<ToolResult> InvokeAsync(JsonElement jsonElement, CancellationToken cancellationToken)
    {
        var query = jsonElement.GetString("query") ?? string.Empty;
        return new ToolResult(await RecallAsync(query, cancellationToken));
    }

    // LLM-callable function (registered with AIFunctionFactory). A clean (string query) signature
    // gives the model a proper "query" parameter, and the JSON string is returned as the tool result.
    public async Task<string> RecallAsync(
        [Description("What to recall from this user's earlier conversations.")] string query,
        CancellationToken cancellationToken)
    {
        var snippets = await RecallSnippetsAsync(query, excludeSessionId: null, cancellationToken);
        var method = _rag.IsEnabled ? "rag" : "keyword";
        return JsonSerializer.Serialize(new { method, count = snippets.Count, snippets });
    }

    // Core recall used by both the tool and the chat endpoint's proactive injection.
    // Flattens every turn across the user's sessions and ranks them by semantic (or keyword) relevance.
    public async Task<IReadOnlyList<string>> RecallSnippetsAsync(string query, string? excludeSessionId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Array.Empty<string>();

        var sessions = await _store.GetAllAsync(_currentUser.UserId);
        var turns = sessions
            .Where(s => excludeSessionId is null || s.Id != excludeSessionId)
            .SelectMany(s => s.History)
            .Select(t => $"{t.Role}: {t.Content}")
            .Distinct()
            .ToList();

        if (turns.Count == 0)
            return Array.Empty<string>();

        if (_rag.IsEnabled)
        {
            var hits = await _rag.SearchAsync(query, turns, _options.TopK, _options.MinScore, cancellationToken);
            var snippets = hits.Select(h => h.Text).ToList();
            _logger.LogInformation("[Memory] RAG recall: {Count} hit(s) for '{Query}'", snippets.Count, query);
            return snippets;
        }

        // Fallback when no embedder is configured: simple case-insensitive term matching.
        var terms = query.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var keywordHits = turns
            .Where(t => terms.Any(term => t.ToLowerInvariant().Contains(term)))
            .Take(_options.TopK)
            .ToList();
        _logger.LogInformation("[Memory] keyword recall: {Count} hit(s) for '{Query}'", keywordHits.Count, query);
        return keywordHits;
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
