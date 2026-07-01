using System.ComponentModel;

namespace ApiGateway.Dtos;

public class ChatRequest
{
    public Guid SessionId { get; }
    public ChatMessage ChatMessage { get; }

    // Which LLM to use: "auto" (Gemini→Ollama fallback, default), "gemini", or "ollama".
    // Set via property (not the constructor) so it stays optional in the request body.
    public string? Provider { get; init; }

    public ChatRequest(ChatMessage chatMessage, Guid sessionId)
    {
        ChatMessage = chatMessage;
        SessionId = sessionId;
    }
}

public class ChatMessage
{
    public ChatMessage(Guid id, string role, string content)
    {
        Id = id;
        Role = role;
        Content = content;
    }

    public Guid Id { get; }
    public string Role { get; }
    public string Content { get; }
}