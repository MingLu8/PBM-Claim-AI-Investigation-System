using ApiGateway.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace ApiGateway.Endpoints;

public class SessionEndpoint : IEndpoint
{
    private readonly ChatClientAgent _agent;
    private readonly ILogger<SessionEndpoint> _logger;

    public SessionEndpoint(
        //ICurrentUser user,
        //ISessionStore store,
        ILogger<SessionEndpoint> logger
        )
    {
        _logger = logger;
    }

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/session", CreateSessionAsync)
           .WithName("Session")
           .WithSummary("Create a chat session")
           .WithDescription("Create a chat session.");
    }

    private async Task<IResult> CreateSessionAsync(
        [FromBody] ChatRequest request,
        CancellationToken token)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var session = new SessionData
        {
            Id = Guid.NewGuid().ToString(),
            CreatedAt = now,
            LastAccessedAt = now,
            UserSub = "Ming"
        };

        return Results.Ok(new SessionResponse(session.Id));
    }
}
