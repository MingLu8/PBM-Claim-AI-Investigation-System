using Microsoft.AspNetCore.Diagnostics;

namespace ApiGateway.Extensions;

using Microsoft.AspNetCore.Diagnostics;
using System.Text.Json;

public static class ExceptionHandlingExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder app)
    {
        app.UseExceptionHandler(errorApp =>
        {
            errorApp.Run(async context =>
            {
                var logger = context.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("GlobalExceptionHandler");

                var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;

                var transactionId = ExtractTransactionId(context);

                logger.LogError(
                    exception,
                    "Unhandled exception Path={Path} TransactionId={TransactionId}",
                    context.Request.Path,
                    transactionId);

                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                context.Response.ContentType = "application/json";

                await context.Response.WriteAsync("{\"error\":\"An unexpected error occurred\"}");
            });
        });

        return app;
    }

    private static string ExtractTransactionId(HttpContext context)
    {
        // 1. Check header
        if (context.Request.Headers.TryGetValue("X-Transaction-Id", out var headerValue))
            return headerValue.ToString();

        // 2. Check query string
        if (context.Request.Query.TryGetValue("transactionId", out var queryValue))
            return queryValue.ToString();

        // 3. Try reading JSON body (only if enabled and safe)
        if (context.Request.ContentType?.Contains("application/json") == true)
        {
            try
            {
                context.Request.EnableBuffering();

                using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
                var body = reader.ReadToEndAsync().Result;

                context.Request.Body.Position = 0;

                if (!string.IsNullOrWhiteSpace(body))
                {
                    using var doc = JsonDocument.Parse(body);
                    if (doc.RootElement.TryGetProperty("transactionId", out var idProp))
                        return idProp.GetString() ?? string.Empty;
                }
            }
            catch
            {
                // swallow — never let logging cause another exception
            }
        }

        return string.Empty;
    }
}
