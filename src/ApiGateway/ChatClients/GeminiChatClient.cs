using System.Text.Json;
using System.Text.Json.Nodes;

namespace ApiGateway.ChatClients;

using AIChatMessage = Microsoft.Extensions.AI.ChatMessage;
using ChatFinishReason = Microsoft.Extensions.AI.ChatFinishReason;

// Custom IChatClient wrapper handling the underlying API payload nuances
public partial class GeminiChatClient : IChatClient
{
    // Schema keywords Gemini's function-declaration parser rejects; stripped before sending.
    private static readonly HashSet<string> UnsupportedSchemaKeys =
        new(StringComparer.Ordinal) { "$schema", "additionalProperties" };

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
    IEnumerable<AIChatMessage> messages,
    ChatOptions? options = null,
    CancellationToken cancellationToken = default)
    {
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent";

        var messageList = messages as IReadOnlyList<AIChatMessage> ?? messages.ToList();

        // Gemini handles system prompts via a dedicated field, NOT a user turn.
        // Pull them out instead of mapping System -> user.
        var systemText = string.Join(
            "\n",
            messageList.Where(m => m.Role == ChatRole.System)
                       .Select(m => m.Text)
                       .Where(t => !string.IsNullOrEmpty(t)));

        var geminiContents = BuildContents(messageList);

        var requestBody = new GeminiRequest
        {
            Contents = geminiContents,
            SystemInstruction = string.IsNullOrEmpty(systemText)
                ? null
                : new GeminiContent { Parts = [new GeminiPart { Text = systemText }] },
            Tools = BuildTools(options)
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

        return BuildChatResponse(geminiResponse);
    }

    // Maps MEAI messages (including the agent's tool-call loop) to Gemini "contents".
    internal static List<GeminiContent> BuildContents(IReadOnlyList<AIChatMessage> messages)
    {
        // Gemini matches a function response to its call by name, so remember the name we
        // emitted for each call id as we walk the conversation in order.
        var callIdToName = new Dictionary<string, string>();
        var contents = new List<GeminiContent>();

        foreach (var message in messages)
        {
            if (message.Role == ChatRole.System)
                continue;

            var parts = new List<GeminiPart>();

            foreach (var content in message.Contents)
            {
                switch (content)
                {
                    case TextContent { Text: { Length: > 0 } text }:
                        parts.Add(new GeminiPart { Text = text });
                        break;

                    case FunctionCallContent call:
                        callIdToName[call.CallId] = call.Name;
                        parts.Add(new GeminiPart
                        {
                            FunctionCall = new GeminiFunctionCall
                            {
                                Name = call.Name,
                                Args = call.Arguments is { Count: > 0 }
                                    ? JsonSerializer.SerializeToElement(call.Arguments)
                                    : null
                            }
                        });
                        break;

                    case FunctionResultContent result:
                        parts.Add(new GeminiPart
                        {
                            FunctionResponse = new GeminiFunctionResponse
                            {
                                Name = callIdToName.TryGetValue(result.CallId, out var name) ? name : result.CallId,
                                Response = WrapFunctionResult(result.Result)
                            }
                        });
                        break;
                }
            }

            // Plain-text messages whose content list wasn't populated still expose .Text.
            if (parts.Count == 0 && !string.IsNullOrEmpty(message.Text))
                parts.Add(new GeminiPart { Text = message.Text });

            if (parts.Count == 0)
                continue;

            // Gemini roles are only "user" or "model"; tool results are sent back as "user".
            contents.Add(new GeminiContent
            {
                Role = message.Role == ChatRole.Assistant ? "model" : "user",
                Parts = parts
            });
        }

        return contents;
    }

    internal static List<GeminiTool>? BuildTools(ChatOptions? options)
    {
        var functions = options?.Tools?.OfType<AIFunction>().ToList();
        if (functions is null || functions.Count == 0)
            return null;

        var declarations = functions
            .Select(f => new GeminiFunctionDeclaration
            {
                Name = f.Name,
                Description = string.IsNullOrWhiteSpace(f.Description) ? null : f.Description,
                Parameters = SanitizeSchema(f.JsonSchema)
            })
            .ToList();

        return [new GeminiTool { FunctionDeclarations = declarations }];
    }

    internal static ChatResponse BuildChatResponse(GeminiResponse? geminiResponse)
    {
        var candidate = geminiResponse?.Candidates is { Count: > 0 } candidates ? candidates[0] : null;

        var contents = new List<AIContent>();
        if (candidate?.Content?.Parts is { Count: > 0 } parts)
        {
            foreach (var part in parts)
            {
                if (part.FunctionCall is { } call)
                {
                    var args = call.Args is { ValueKind: JsonValueKind.Object } argsElement
                        ? argsElement.Deserialize<Dictionary<string, object?>>()
                        : null;

                    // Gemini doesn't issue call ids; synthesize one so the result can round-trip.
                    contents.Add(new FunctionCallContent(Guid.NewGuid().ToString("N"), call.Name, args));
                }
                else if (!string.IsNullOrEmpty(part.Text))
                {
                    contents.Add(new TextContent(part.Text));
                }
            }
        }

        if (contents.Count == 0)
            contents.Add(new TextContent("No response generated."));

        var hasToolCalls = contents.Any(c => c is FunctionCallContent);
        return new ChatResponse(new AIChatMessage(ChatRole.Assistant, contents))
        {
            FinishReason = hasToolCalls ? ChatFinishReason.ToolCalls : ChatFinishReason.Stop
        };
    }

    // Gemini requires functionResponse.response to be a JSON object. Our tools return JSON
    // strings, so pass an object through as-is and wrap anything else under "result".
    internal static JsonElement WrapFunctionResult(object? result)
    {
        var raw = result as string ?? JsonSerializer.Serialize(result);

        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
                return doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            // Not JSON; fall through to wrapping.
        }

        return JsonSerializer.SerializeToElement(new { result = raw });
    }

    // Trim schema keywords Gemini rejects and omit empty object schemas (parameterless tools).
    internal static JsonElement? SanitizeSchema(JsonElement schema)
    {
        if (schema.ValueKind != JsonValueKind.Object)
            return null;

        var node = JsonNode.Parse(schema.GetRawText());
        if (node is null)
            return null;

        StripUnsupportedKeys(node);

        if (node is JsonObject obj)
        {
            var typeIsObject = obj.TryGetPropertyValue("type", out var typeNode)
                && typeNode is JsonValue typeValue
                && typeValue.TryGetValue<string>(out var typeStr)
                && typeStr == "object";

            var hasProperties = obj.TryGetPropertyValue("properties", out var propsNode)
                && propsNode is JsonObject props
                && props.Count > 0;

            if (typeIsObject && !hasProperties)
                return null;
        }

        return JsonSerializer.SerializeToElement(node);
    }

    private static void StripUnsupportedKeys(JsonNode node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var key in obj.Select(kv => kv.Key).ToList())
                {
                    if (UnsupportedSchemaKeys.Contains(key))
                        obj.Remove(key);
                    else if (obj[key] is { } child)
                        StripUnsupportedKeys(child);
                }
                break;

            case JsonArray array:
                foreach (var item in array)
                    if (item is not null)
                        StripUnsupportedKeys(item);
                break;
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => serviceType.IsInstanceOfType(this) ? this : null;
    public void Dispose() => _httpClient.Dispose();


    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<AIChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
