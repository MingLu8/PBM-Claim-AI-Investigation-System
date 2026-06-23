//using Microsoft.Extensions.Hosting;
//using Microsoft.Extensions.Logging;
//using Microsoft.SemanticKernel;
//using Microsoft.SemanticKernel.ChatCompletion;
//using System.Threading.Channels;

//namespace ApiGateway;

//public class ChannelInvestigationWorker(
//    Channel<string> claimQueue,
//    Kernel kernel,
//    DistributedInvestigationState stateManager,
//    ILogger<ChannelInvestigationWorker> logger) : BackgroundService
//{
//    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
//    {
//        logger.LogInformation("Channel Worker started. Waiting for claims...");
//        var chatCompletion = kernel.GetRequiredService<IChatCompletionService>();

//        var settings = new PromptExecutionSettings
//        {
//            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
//        };

//        // Asynchronously wait for claims to be published to the channel
//        await foreach (var rawClaim in claimQueue.Reader.ReadAllAsync(stoppingToken))
//        {
//            // For this example, we'll hash the claim to create a deterministic ID
//            var claimId = rawClaim.GetHashCode().ToString();
//            logger.LogInformation("Processing Claim ID: {ClaimId}", claimId);

//            var chatHistory = await stateManager.LoadStateAsync(claimId, stoppingToken);
//            chatHistory.AddUserMessage($"Analyze this incoming NCPDP payload: {rawClaim}");

//            var result = await chatCompletion.GetChatMessageContentAsync(chatHistory, settings, kernel, stoppingToken);
//            chatHistory.Add(result);

//            if (!result.Items.OfType<FunctionCallContent>().Any())
//            {
//                logger.LogInformation("Final Ruling for {ClaimId}: {Ruling}", claimId, result.Content);
//            }

//            await stateManager.SaveStateAsync(claimId, chatHistory, stoppingToken);
//        }
//    }
//}

