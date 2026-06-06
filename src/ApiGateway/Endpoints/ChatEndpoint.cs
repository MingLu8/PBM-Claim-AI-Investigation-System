using Microsoft.AspNetCore.Mvc;

namespace ApiGateway.Endpoints;

public class ChatEndpoint : IEndpoint
{
    private readonly ChatClientAgent _agent;
    private readonly ILogger<ChatEndpoint> _logger;

    public ChatEndpoint(
        ChatClientAgent agent,
        ILogger<ChatEndpoint> logger
        )
    {
        _agent = agent;
        _logger = logger;
    }

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/chat", Chat)
           .WithName("Chat")
           .WithSummary("Chat with the AI model")
           .WithDescription("Sends a message to the AI model and returns the response.");
    }

    private async Task<IResult> Chat(
        [FromBody] string prompt,
        CancellationToken token)
    {
        _logger.LogInformation("InvestigationWorker is starting.");
        // var prompt = "You are a PBM Investigation Agent. Acknowledge initialization and state your purpose in one sentence.";

        try
        {
            var rawClaim = "HEADDATA...201-B11234567890...TAILDATA";
            prompt = $"I have a flagged claim payload: {rawClaim}. What is the pharmacy NPI?";
            var response = await _agent.RunAsync(prompt);

            return Results.Ok(new { message = response.Text });

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while running the InvestigationWorker.");
            return Results.InternalServerError(ex);
        }
    }


}
