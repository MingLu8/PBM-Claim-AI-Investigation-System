using Microsoft.AspNetCore.Mvc;

namespace ApiGateway.Modules;

public static class ClaimsModule
{
    public static void MapClaimEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/adjudicate", AdjudicateClaim)
           .WithName("AdjudicateClaim")
           .WithSummary("Adjudicate a Pharmacy Claim")
           .WithDescription("Accepts raw NCPDP D.0 string, processes it via Kafka, and returns the response.");
    }

    private static async Task<IResult> AdjudicateClaim(
        [FromBody] string ncpdp,
        HttpContext ctx,
        ILoggerFactory loggerFactory,
        CancellationToken token)
    {
        var logger = loggerFactory.CreateLogger("ClaimsModule");
        var remoteIp = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var transationId = Guid.NewGuid().ToString();
        logger.LogInformation("Adjudicate request received RemoteIp={RemoteIp}, TransactionId={transationId}", remoteIp, transationId);

        //var ncpdp = await ReadRequestBodyAsync(ctx, logger);
        if (ncpdp is null)
            return Results.StatusCode(400);

        logger.LogDebug("Request payload length Length={Length} bytes", ncpdp.Length);

        try
        {
            logger.LogInformation("Adjudication completed RemoteIp={RemoteIp}", remoteIp);

            return Results.Ok(new { transationId });
        }
        catch (TimeoutException)
        {
            logger.LogWarning("Adjudication timeout RemoteIp={RemoteIp}", remoteIp);
            return Results.StatusCode(504);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            logger.LogInformation("Adjudication canceled by client RemoteIp={RemoteIp}", remoteIp);
            return Results.StatusCode(499);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected adjudication error RemoteIp={RemoteIp}", remoteIp);
            return Results.StatusCode(500);
        }
    }
}