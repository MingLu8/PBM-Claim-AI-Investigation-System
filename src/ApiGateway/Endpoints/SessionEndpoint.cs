using ApiGateway.Dtos;
using ApiGateway.Services;
using Microsoft.AspNetCore.Mvc;

namespace ApiGateway.Endpoints;

public class SessionEndpoint : IEndpoint
{
    private readonly ICurrentUser _user;
    private readonly ISessionStore _store;
    private readonly ILogger<SessionEndpoint> _logger;

    public SessionEndpoint(
        ICurrentUser user,
        ISessionStore store,
        ILogger<SessionEndpoint> logger
        )
    {
        _user = user;
        _store = store;
        _logger = logger;
    }

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/session", CreateSessionAsync)
           .WithName("CreateSession")
           .WithSummary("Create a chat session")
           .WithDescription("Create a chat session.");

        app.MapGet("/api/sessions", GetSessionsAsync)
           .WithName("GetSessions")
           .WithSummary("Get all chat sessions for user")
           .WithDescription("Get all chat sessions for user.");

        app.MapGet("/api/sessions/{id}", GetSessionAsync)
          .WithName("GetSession")
          .WithSummary("Get chat session by id")
          .WithDescription("Get chat session by id.");

        app.MapDelete("/api/sessions/{id}", DeleteSessionAsync)
          .WithName("DeleteSession")
          .WithSummary("Delete chat session by id")
          .WithDescription("Delete chat session by id.");
    }

    private async Task<IResult> DeleteSessionAsync(string id)
    {
        var session = await _store.GetAsync(id);
        if (session is null) return Results.NotFound(new { error = "Session not found." });

        if (session.User != _user.UserId)
            return Results.StatusCode(StatusCodes.Status403Forbidden);

        await _store.DeleteAsync(id);
        return Results.NoContent();
    }

    private async Task<IResult> GetSessionAsync(string id)
    {
        var session = await _store.GetAsync(id);
        if (session == null) return Results.NotFound(new { error = "Session not found." });

        if (!string.IsNullOrEmpty(session.User) && session.User != _user.UserId)
            return Results.StatusCode(StatusCodes.Status403Forbidden);

        return Results.Ok(session);
    }

    private async Task<IResult> GetSessionsAsync()
    {
        //if (string.IsNullOrEmpty(_user.UserId)) return Results.Ok(Enumerable.Empty<SessionSummary>());

        var sessions = await _store.GetAllAsync(_user.UserId);
        var summaries = sessions.Where(a => a.History.Count() > 0)
            .Select(a =>
            {
                var firstUser = a.History.FirstOrDefault(b => b.Role == "user")?.Content ?? "New chat";
                var title = firstUser.Length > 60 ? firstUser[..60] + "..." : firstUser;
                return new SessionSummary(a.Id, title, a.LastAccessAt, a.History.Count());
            }).ToList();
        return Results.Ok(sessions);
    }

    private async Task<IResult> CreateSessionAsync(
        [FromBody] ChatRequest request,
        CancellationToken token)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var session = new SessionData(Guid.NewGuid().ToString(), now, now, _user.Sub ?? _user.Email, Enumerable.Empty<ChatTurn>());
        await _store.SetASync(session);
        return Results.Ok(new SessionResponse(session.Id));
    }


}
