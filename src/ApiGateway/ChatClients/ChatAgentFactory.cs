using ApiGateway.Plugins;

namespace ApiGateway.ChatClients;

/// <summary>Supported LLM provider selectors sent by the client (case-insensitive).</summary>
public static class LlmProvider
{
    public const string Auto = "auto";     // Gemini, falling back to local Ollama on failure
    public const string Gemini = "gemini"; // force Google Gemini
    public const string Ollama = "ollama"; // force the local Ollama model

    public static readonly IReadOnlyList<string> All = [Auto, Gemini, Ollama];
}

public interface IChatAgentFactory
{
    /// <summary>Builds a PBM agent backed by the requested provider ("auto" | "gemini" | "ollama").</summary>
    ChatClientAgent Create(string? provider);
}

public sealed class ChatAgentFactory : IChatAgentFactory
{
    private readonly IServiceProvider _sp;
    private readonly OllamaSettings _ollama;
    private readonly AgentSettings _agent;

    public ChatAgentFactory(IServiceProvider sp, OllamaSettings ollama, AgentSettings agent)
    {
        _sp = sp;
        _ollama = ollama;
        _agent = agent;
    }

    public ChatClientAgent Create(string? provider)
    {
        var chatClient = BuildClient(provider);

        // Tools are resolved per request (scoped) so they share the caller's DI scope.
        var npiPlugin = _sp.GetRequiredService<PharmacyNpiParser>();
        var cardPlugin = _sp.GetRequiredService<CardHolderIdParser>();
        var memoryTool = _sp.GetRequiredService<RecallMemoryTool>();

        // Persona comes from the "Agent" config section (see AgentSettings). Lines are joined
        // with '\n' and the "{memoryTool}" token is replaced with the tool's actual name.
        var instructions = string.Join("\n", _agent.Instructions)
            .Replace("{memoryTool}", memoryTool.Definition.FunctionName);

        return new ChatClientAgent(
            chatClient,
            name: _agent.Name,
            instructions: instructions,
            tools: new List<AITool>
            {
                AIFunctionFactory.Create(npiPlugin.ExtractPharmacyNpi, name: "extract_pharmacy_npi"),
                AIFunctionFactory.Create(cardPlugin.ExtractCardholderId, name: "extract_cardholder_id"),
                AIFunctionFactory.Create(memoryTool.RecallAsync, name: memoryTool.Definition.FunctionName, description: memoryTool.Definition.FunctionDescription)
            });
    }

    // "gemini" → Gemini only; "ollama" → local model only; anything else → the registered
    // FallbackChatClient (Gemini with Ollama fallback), which is the "auto" default.
    private IChatClient BuildClient(string? provider) => provider?.Trim().ToLowerInvariant() switch
    {
        LlmProvider.Gemini => _sp.GetRequiredService<GeminiChatClient>(),
        LlmProvider.Ollama => new OllamaChatClient(new Uri(_ollama.Endpoint), _ollama.Model),
        _ => _sp.GetRequiredService<IChatClient>(),
    };
}
