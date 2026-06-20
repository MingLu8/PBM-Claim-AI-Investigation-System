using System.ComponentModel;

namespace ApiGateway.Dtos;

public class ChatRequest
{
    public Guid SessionId { get; }
    public ChatMessage ChatMessage { get; }
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