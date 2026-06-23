using AIChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace ApiGateway.ChatClients;

/// <summary>
/// Tries the primary IChatClient; on any exception falls back to a local Ollama instance.
/// </summary>
public sealed class FallbackChatClient : IChatClient
{
    private readonly IChatClient _primary;
    private readonly IChatClient _fallback;
    private readonly ILogger<FallbackChatClient> _logger;
    private readonly string _primaryName;
    private readonly string _fallbackName;

    public FallbackChatClient(IChatClient primary, IChatClient fallback, ILogger<FallbackChatClient> logger,
        string primaryName = "Primary", string fallbackName = "Ollama")
    {
        _primary = primary;
        _fallback = fallback;
        _logger = logger;
        _primaryName = primaryName;
        _fallbackName = fallbackName;
    }

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<AIChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _primary.GetResponseAsync(messages, options, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "[ChatClient] '{Primary}' failed — falling back to '{Fallback}'.",
                _primaryName, _fallbackName);

            return await _fallback.GetResponseAsync(messages, options, cancellationToken);
        }
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<AIChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return StreamWithFallbackAsync(messages, options, cancellationToken);
    }

    private async IAsyncEnumerable<ChatResponseUpdate> StreamWithFallbackAsync(
        IEnumerable<AIChatMessage> messages,
        ChatOptions? options,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Buffer the primary stream so we can fall back cleanly (yield inside try/catch is illegal in C#).
        List<ChatResponseUpdate>? primaryUpdates = null;
        Exception? primaryException = null;

        try
        {
            primaryUpdates = [];
            await foreach (var update in _primary.GetStreamingResponseAsync(messages, options, cancellationToken))
                primaryUpdates.Add(update);
        }
        catch (Exception ex)
        {
            primaryException = ex;
            primaryUpdates = null;
        }

        if (primaryUpdates is not null)
        {
            foreach (var update in primaryUpdates)
                yield return update;
        }
        else
        {
            _logger.LogWarning(primaryException,
                "[ChatClient] '{Primary}' streaming failed — falling back to '{Fallback}'.",
                _primaryName, _fallbackName);

            await foreach (var update in _fallback.GetStreamingResponseAsync(messages, options, cancellationToken))
                yield return update;
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
        => _primary.GetService(serviceType, serviceKey) ?? _fallback.GetService(serviceType, serviceKey);

    public void Dispose()
    {
        _primary.Dispose();
        _fallback.Dispose();
    }
}
