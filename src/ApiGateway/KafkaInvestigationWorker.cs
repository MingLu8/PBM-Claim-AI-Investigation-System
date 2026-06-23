//using Confluent.Kafka;
//using Confluent.Kafka.Admin;
//using Microsoft.SemanticKernel.ChatCompletion;

//namespace ApiGateway;

//public class KafkaInvestigationWorker(
//    Kernel kernel,
//    DistributedInvestigationState stateManager,
//    ILogger<KafkaInvestigationWorker> logger) : BackgroundService
//{
//    private readonly ConsumerConfig _config = new()
//    {
//        BootstrapServers = "localhost:9092",
//        GroupId = "fwa-investigation-group",
//        AutoOffsetReset = AutoOffsetReset.Earliest
//    };

//    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
//    {
//        EnsureTopicExists("pbm-incoming-claims", _config.BootstrapServers).Wait(stoppingToken);
//        logger.LogInformation("Kafka Worker started. Subscribing to claims topic...");
//        var chatCompletion = kernel.GetRequiredService<IChatCompletionService>();

//        var settings = new PromptExecutionSettings
//        {
//            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
//        };

//        using var consumer = new ConsumerBuilder<string, string>(_config).Build();
//        consumer.Subscribe("pbm-incoming-claims");

//        try
//        {
//            while (!stoppingToken.IsCancellationRequested)
//            {
//                // Pull the next message from Kafka
//                var consumeResult = consumer.Consume(stoppingToken);
//                var claimId = consumeResult.Message.Key; // e.g., Transaction ID
//                var rawClaim = consumeResult.Message.Value;

//                logger.LogInformation("Processing Kafka Claim ID: {ClaimId}", claimId);

//                // 1. Load distributed state
//                var chatHistory = await stateManager.LoadStateAsync(claimId, stoppingToken);

//                // 2. Add the new event
//                chatHistory.AddUserMessage($"Analyze this incoming NCPDP payload: {rawClaim}");

//                // 3. Let Ollama reason and use tools
//                var result = await chatCompletion.GetChatMessageContentAsync(chatHistory, settings, kernel, stoppingToken);
//                chatHistory.Add(result);

//                if (!result.Items.OfType<FunctionCallContent>().Any())
//                {
//                    logger.LogInformation("Final Ruling for {ClaimId}: {Ruling}", claimId, result.Content);
//                }

//                // 4. Save state back to Redis
//                await stateManager.SaveStateAsync(claimId, chatHistory, stoppingToken);

//                // Commit the offset so Kafka knows we successfully processed it
//                consumer.Commit(consumeResult);
//            }
//        }
//        catch (OperationCanceledException)
//        {
//            // Graceful shutdown
//            consumer.Close();
//        }
//    }

//public async Task EnsureTopicExists(string topicName, string bootstrapServers)
//{
//    using var adminClient = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = bootstrapServers }).Build();

//    try
//    {
//        var metadata = adminClient.GetMetadata(TimeSpan.FromSeconds(5));
//        var topicExists = metadata.Topics.Any(t => t.Topic == topicName);

//        if (!topicExists)
//        {
//            await adminClient.CreateTopicsAsync(new TopicSpecification[] {
//                new TopicSpecification { Name = topicName, ReplicationFactor = 1, NumPartitions = 3 }
//            });
//        }
//    }
//    catch (CreateTopicsException e)
//    {
//        // Handle cases where another instance created it simultaneously
//        Console.WriteLine($"Topic creation status: {e.Results[0].Error.Reason}");
//    }
//}
//}

