using ApiGateway.ChatClients;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Text;

namespace PBM.Infrastructure.Tests
{

    public class GeminiIntegrationTests
    {
        private readonly IConfiguration _configuration;

        public GeminiIntegrationTests()
        {
            // Setup configuration builder to pull from the local test directory
            _configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("secrets.json", optional: false)
                .Build();
        }

        [Fact]
        [Trait("Category", "Integration")] // Allows separating these from quick unit tests
        public async Task RealClient_CanConnectToGeminiApi_UsingSecretsFile()
        {

            // 1. Arrange: Read from the real configuration payload
            var apiKey = _configuration["Gemini:ApiKey"];
            var model = _configuration["Gemini:Model"];
            var settings = _configuration.GetSection("Gemini").Get<GeminiSettings>();
            
            
            Assert.False(string.IsNullOrEmpty(apiKey), "Test aborted: GeminiApiKey is missing from secrets.json");

            // Instantiate the real client pointing to Google's live server
            using var client = new GeminiChatClient(settings);
            var messages = new List<ChatMessage> { new ChatMessage(ChatRole.User, "What is NDC?") };

            // 2. Act
            var response = await client.GetResponseAsync(messages.AsEnumerable());

            // 3. Assert
            Assert.NotNull(response);
            Assert.NotNull(response.Messages);
            Console.WriteLine($"Live Gemini Response: {response.Messages.First()}");
        }
    }
}
