namespace ApiGateway;

public class InvestigationWorker : BackgroundService
{
    private readonly Kernel _kernel;
    private readonly ILogger<InvestigationWorker> _logger;

    public InvestigationWorker(
        Kernel kernel,
        ILogger<InvestigationWorker> logger)
    {
        _kernel = kernel;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("InvestigationWorker is starting.");
        var prompt = "You are a PBM Investigation Agent. Acknowledge initialization and state your purpose in one sentence.";

        try
        {
            var response = await _kernel.InvokePromptAsync(prompt);
            _logger.LogInformation("InvestigationWorker received response: {Response}", response);

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while running the InvestigationWorker.");
        }
    }
}
