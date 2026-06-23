using ApiGateway.Dtos;
using ApiGateway.Plugins;
using ApiGateway.Services;
using Microsoft.AspNetCore.Mvc;

namespace ApiGateway.Endpoints;

using AIChatMessage = Microsoft.Extensions.AI.ChatMessage;

public class ChatEndpoint : IEndpoint
{
    private readonly ILogger<ChatEndpoint> _logger;

    public ChatEndpoint(ILogger<ChatEndpoint> logger)
    {
        _logger = logger;
    }

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/chat", Chat)
           .WithName("Chat")
           .WithSummary("Chat with the AI model")
           .WithDescription("Sends a message to the AI model and returns the response.");
    }

    private async Task<IResult> Chat(
        [FromBody] ChatRequest request,
        ChatClientAgent agent,
        ISessionStore store,
        ICurrentUser user,
        RecallMemoryTool memory,
        CancellationToken token)
    {
        try
        {
            var userInput = request.ChatMessage.Content ?? string.Empty;
            var userId = user.UserId;

            // Resolve (or lazily create) the session this message belongs to.
            var sessionId = request.SessionId == Guid.Empty
                ? Guid.NewGuid().ToString()
                : request.SessionId.ToString();
            var session = await store.GetAsync(sessionId);
            var history = session?.History.ToList() ?? new List<ChatTurn>();

            var messages = new List<AIChatMessage>();

            // 1. Always inject cross-session memory status so the model never claims it has no
            //    memory capability — even an empty result tells it "nothing was found" rather
            //    than leaving it with no context at all.
            if (memory.IsEnabled)
            {
                var snippets = await memory.RecallSnippetsAsync(userInput, excludeSessionId: sessionId, token);
                string memoryText;
                if (snippets.Count > 0)
                {
                    memoryText = "MEMORY (from this user's earlier sessions):\n" +
                                 string.Join("\n", snippets.Select(s => "- " + s));
                    _logger.LogInformation("[Memory] Injected {Count} recalled snippet(s) into chat context.", snippets.Count);
                }
                else
                {
                    memoryText = "MEMORY: No relevant facts found in this user's past sessions for this query.";
                    _logger.LogInformation("[Memory] No snippets recalled for query.");
                }
                messages.Add(new AIChatMessage(ChatRole.System, memoryText));
            }

            // 2. Replay this session's own history for in-conversation continuity.
            foreach (var turn in history)
            {
                var role = string.Equals(turn.Role, "assistant", StringComparison.OrdinalIgnoreCase)
                    ? ChatRole.Assistant
                    : ChatRole.User;
                messages.Add(new AIChatMessage(role, turn.Content));
            }

            // 3. The new user message. (The agent's own tools, incl. recall_past_conversations,
            //    remain available for the model to call agentically.)
            messages.Add(new AIChatMessage(ChatRole.User, userInput));

            var response = await agent.RunAsync(messages, cancellationToken: token);
            var replyText = response.Text;

            // 4. Persist the exchange so future sessions can recall it.
            history.Add(new ChatTurn("user", userInput));
            history.Add(new ChatTurn("assistant", replyText));

            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var updated = new SessionData(
                sessionId,
                session?.CreatedAt ?? now,
                now,
                string.IsNullOrEmpty(session?.User) ? userId : session.User,
                history);
            await store.SetASync(updated);

            return Results.Ok(new { sessionId, message = replyText });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while running the chat endpoint.");
            return Results.InternalServerError(ex);
        }
    }
}
