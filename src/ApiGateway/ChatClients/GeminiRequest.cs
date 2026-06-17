using System.Text.Json.Serialization;

namespace ApiGateway.ChatClients;

public sealed class GeminiRequest
{
    [JsonPropertyName("contents")]
    public List<GeminiContent> Contents { get; set; } = [];

    // Gemini's dedicated system-prompt slot. Omitted from the payload when null.
    [JsonPropertyName("systemInstruction")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GeminiContent? SystemInstruction { get; set; }

    // Optional: maps from ChatOptions (temperature, max tokens, etc.)
    [JsonPropertyName("generationConfig")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GeminiGenerationConfig? GenerationConfig { get; set; }
}

public sealed class GeminiContent
{
    // System instruction has no role, so omit it when not set.
    [JsonPropertyName("role")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Role { get; set; }

    [JsonPropertyName("parts")]
    public List<GeminiPart> Parts { get; set; } = [];
}

public sealed class GeminiPart
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}

public sealed class GeminiGenerationConfig
{
    [JsonPropertyName("temperature")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public float? Temperature { get; set; }

    [JsonPropertyName("maxOutputTokens")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MaxOutputTokens { get; set; }

    [JsonPropertyName("topP")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public float? TopP { get; set; }
}

public sealed class GeminiResponse
{
    [JsonPropertyName("candidates")]
    public List<GeminiCandidate>? Candidates { get; set; }

    [JsonPropertyName("promptFeedback")]
    public GeminiPromptFeedback? PromptFeedback { get; set; }
}

public sealed class GeminiCandidate
{
    [JsonPropertyName("content")]
    public GeminiContent? Content { get; set; }

    // Useful to log: "STOP", "MAX_TOKENS", "SAFETY", "RECITATION"
    [JsonPropertyName("finishReason")]
    public string? FinishReason { get; set; }
}

public sealed class GeminiPromptFeedback
{
    [JsonPropertyName("blockReason")]
    public string? BlockReason { get; set; }
}