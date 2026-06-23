using ApiGateway.ChatClients;
using ApiGateway.Dtos;
using ApiGateway.Plugins;
using ApiGateway.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;

using AIChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace PBM.Infrastructure.Tests;

public class MemoryRecallTests
{
    // ---- InMemoryRagSearch (semantic ranking) ----

    [Fact]
    public async Task RagSearch_RanksMostRelevantTurnFirst_AndFiltersByMinScore()
    {
        var rag = new InMemoryRagSearch(new FakeEmbedder());
        var docs = new[]
        {
            "user: my member id is 12345",
            "assistant: ndc is the national drug code",
            "user: what is a formulary"
        };

        var hits = (await rag.SearchAsync("member id", docs, topK: 5, minScore: 0.2, CancellationToken.None)).ToList();

        Assert.NotEmpty(hits);
        Assert.Equal("user: my member id is 12345", hits[0].Text);
        // Unrelated turns score 0 against the query and are dropped by minScore.
        Assert.DoesNotContain(hits, h => h.Text.Contains("formulary"));
    }

    [Fact]
    public async Task RagSearch_RespectsTopK()
    {
        var rag = new InMemoryRagSearch(new FakeEmbedder());
        var docs = new[]
        {
            "user: member id one",
            "user: member id two",
            "user: member id three"
        };

        var hits = (await rag.SearchAsync("member id", docs, topK: 2, minScore: 0.1, CancellationToken.None)).ToList();

        Assert.Equal(2, hits.Count);
    }

    [Fact]
    public async Task RagSearch_Disabled_WhenEmbedderUnavailable()
    {
        var rag = new InMemoryRagSearch(new FakeEmbedder { Enabled = false });

        Assert.False(rag.IsEnabled);
        var hits = await rag.SearchAsync("member id", new[] { "user: member id" }, 5, 0.2, CancellationToken.None);
        Assert.Empty(hits);
    }

    // ---- RecallMemoryTool (cross-session aggregation) ----

    [Fact]
    public async Task Recall_AggregatesAcrossSessions_ExcludesCurrentSession_AndOtherUsers()
    {
        var current = new SessionData("s-current", 1, 100, "user-1",
            new[] { new ChatTurn("user", "member id in the current session") });
        var past = new SessionData("s-past", 1, 50, "user-1",
            new[] { new ChatTurn("user", "member id is 999 from a past chat") });
        var otherUser = new SessionData("s-other", 1, 60, "user-2",
            new[] { new ChatTurn("user", "member id secret of another user") });

        var tool = NewTool(new FakeEmbedder(), current, past, otherUser);

        var snippets = await tool.RecallSnippetsAsync("member id", excludeSessionId: "s-current", CancellationToken.None);

        Assert.Contains(snippets, s => s.Contains("999"));               // recalled from the other user-1 session
        Assert.DoesNotContain(snippets, s => s.Contains("current"));     // current session excluded
        Assert.DoesNotContain(snippets, s => s.Contains("another user")); // other user's data never leaks
    }

    [Fact]
    public async Task Recall_FallsBackToKeyword_WhenEmbedderDisabled()
    {
        var past = new SessionData("s-past", 1, 50, "user-1",
            new[] { new ChatTurn("user", "member id is 999 from a past chat") });

        var tool = NewTool(new FakeEmbedder { Enabled = false }, past);

        var snippets = await tool.RecallSnippetsAsync("999", excludeSessionId: null, CancellationToken.None);

        Assert.Contains(snippets, s => s.Contains("999"));
    }

    [Fact]
    public async Task Recall_ReturnsEmpty_ForBlankQuery()
    {
        var past = new SessionData("s-past", 1, 50, "user-1",
            new[] { new ChatTurn("user", "member id is 999") });
        var tool = NewTool(new FakeEmbedder(), past);

        var snippets = await tool.RecallSnippetsAsync("   ", excludeSessionId: null, CancellationToken.None);

        Assert.Empty(snippets);
    }

    private static RecallMemoryTool NewTool(IEmbedder embedder, params SessionData[] sessions) =>
        new(new FakeSessionStore(sessions),
            new FakeCurrentUser(),
            new InMemoryRagSearch(embedder),
            new MemoryOptions(),
            NullLogger<RecallMemoryTool>.Instance);

    // ---- Gemini tool-calling protocol mapping ----

    [Fact]
    public void BuildTools_SanitizesSchema_StrippingUnsupportedKeywords()
    {
        var function = AIFunctionFactory.Create(
            ([System.ComponentModel.Description("what to recall")] string query) => query,
            name: "recall_past_conversations");
        var options = new ChatOptions { Tools = new List<AITool> { function } };

        var tools = GeminiChatClient.BuildTools(options);

        Assert.NotNull(tools);
        var declaration = tools!.Single().FunctionDeclarations.Single();
        Assert.Equal("recall_past_conversations", declaration.Name);

        var schema = declaration.Parameters!.Value;
        Assert.False(HasPropertyDeep(schema, "$schema"), "$schema must be stripped for Gemini");
        Assert.False(HasPropertyDeep(schema, "additionalProperties"), "additionalProperties must be stripped for Gemini");
        Assert.True(schema.GetProperty("properties").TryGetProperty("query", out _));
    }

    [Fact]
    public void BuildChatResponse_ParsesFunctionCall_IntoFunctionCallContent()
    {
        var response = new GeminiResponse
        {
            Candidates = new()
            {
                new GeminiCandidate
                {
                    Content = new GeminiContent
                    {
                        Role = "model",
                        Parts = new()
                        {
                            new GeminiPart
                            {
                                FunctionCall = new GeminiFunctionCall
                                {
                                    Name = "recall_past_conversations",
                                    Args = JsonSerializer.SerializeToElement(new { query = "member id" })
                                }
                            }
                        }
                    }
                }
            }
        };

        var chatResponse = GeminiChatClient.BuildChatResponse(response);

        var call = chatResponse.Messages.Single().Contents.OfType<FunctionCallContent>().Single();
        Assert.Equal("recall_past_conversations", call.Name);
        Assert.Equal("member id", call.Arguments!["query"]?.ToString());
        Assert.Equal(ChatFinishReason.ToolCalls, chatResponse.FinishReason);
    }

    [Fact]
    public void BuildChatResponse_ParsesText_WithStopReason()
    {
        var response = new GeminiResponse
        {
            Candidates = new()
            {
                new GeminiCandidate
                {
                    Content = new GeminiContent { Parts = new() { new GeminiPart { Text = "NDC means National Drug Code." } } }
                }
            }
        };

        var chatResponse = GeminiChatClient.BuildChatResponse(response);

        Assert.Equal("NDC means National Drug Code.", chatResponse.Text);
        Assert.Equal(ChatFinishReason.Stop, chatResponse.FinishReason);
    }

    [Fact]
    public void BuildContents_MapsToolLoop_FunctionCallAndResponse()
    {
        var messages = new List<AIChatMessage>
        {
            new(ChatRole.System, "you are an assistant"),         // pulled out into systemInstruction, not contents
            new(ChatRole.User, "find my member id"),
            new(ChatRole.Assistant, new List<AIContent>
            {
                new FunctionCallContent("call-1", "recall_past_conversations",
                    new Dictionary<string, object?> { ["query"] = "member id" })
            }),
            new(ChatRole.Tool, new List<AIContent>
            {
                new FunctionResultContent("call-1", "{\"snippets\":[\"member id is 999\"]}")
            })
        };

        var contents = GeminiChatClient.BuildContents(messages);

        Assert.Equal(3, contents.Count);                          // system message excluded
        Assert.Equal("user", contents[0].Role);
        Assert.Equal("model", contents[1].Role);
        Assert.Equal("recall_past_conversations", contents[1].Parts[0].FunctionCall!.Name);

        // Tool results go back to Gemini as a "user" turn whose functionResponse name matches the call.
        Assert.Equal("user", contents[2].Role);
        var functionResponse = contents[2].Parts[0].FunctionResponse!;
        Assert.Equal("recall_past_conversations", functionResponse.Name);
        Assert.Equal(JsonValueKind.Object, functionResponse.Response.ValueKind); // Gemini requires an object
    }

    [Fact]
    public void WrapFunctionResult_PassesJsonObjectThrough()
    {
        var wrapped = GeminiChatClient.WrapFunctionResult("{\"count\":2}");
        Assert.Equal(JsonValueKind.Object, wrapped.ValueKind);
        Assert.Equal(2, wrapped.GetProperty("count").GetInt32());
    }

    [Fact]
    public void WrapFunctionResult_WrapsNonJsonUnderResultKey()
    {
        var wrapped = GeminiChatClient.WrapFunctionResult("plain text");
        Assert.Equal(JsonValueKind.Object, wrapped.ValueKind);
        Assert.Equal("plain text", wrapped.GetProperty("result").GetString());
    }

    private static bool HasPropertyDeep(JsonElement element, string name)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    if (prop.Name == name) return true;
                    if (HasPropertyDeep(prop.Value, name)) return true;
                }
                return false;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                    if (HasPropertyDeep(item, name)) return true;
                return false;
            default:
                return false;
        }
    }
}

// ---- Test doubles ----

file sealed class FakeEmbedder : IEmbedder
{
    // Tiny deterministic bag-of-words embedding so cosine similarity reflects term overlap.
    private static readonly string[] Vocabulary =
        { "member", "id", "ndc", "drug", "formulary", "npi", "pharmacy", "copay", "999", "one", "two", "three" };

    public bool Enabled { get; init; } = true;
    public bool IsEnabled => Enabled;

    public Task<float[]?> EmbedAsync(string text, CancellationToken cancellationToken)
    {
        var lower = text.ToLowerInvariant();
        var vector = new float[Vocabulary.Length];
        for (var i = 0; i < Vocabulary.Length; i++)
            vector[i] = lower.Contains(Vocabulary[i]) ? 1f : 0f;
        return Task.FromResult<float[]?>(vector);
    }
}

file sealed class FakeSessionStore : ISessionStore
{
    private readonly List<SessionData> _sessions;
    public FakeSessionStore(IEnumerable<SessionData> sessions) => _sessions = sessions.ToList();

    public Task<IEnumerable<SessionData>> GetAllAsync(string user) =>
        Task.FromResult(_sessions.Where(s => s.User == user).OrderByDescending(s => s.LastAccessAt).AsEnumerable());

    public Task<SessionData?> GetAsync(string id) => Task.FromResult(_sessions.FirstOrDefault(s => s.Id == id));
    public Task<bool> ExistsAsync(string id) => Task.FromResult(_sessions.Any(s => s.Id == id));
    public Task SetASync(SessionData sessionData)
    {
        _sessions.RemoveAll(s => s.Id == sessionData.Id);
        _sessions.Add(sessionData);
        return Task.CompletedTask;
    }
    public Task DeleteAsync(string id)
    {
        _sessions.RemoveAll(s => s.Id == id);
        return Task.CompletedTask;
    }
}

file sealed class FakeCurrentUser : ICurrentUser
{
    public bool IsAuthenticated => true;
    public string? Sub => "user-1";
    public string Name => "Test User";
    public string Email => "test@example.com";
    public string UserId => Sub ?? Email;
    public UserGroups Groups => new(true, false);
}
