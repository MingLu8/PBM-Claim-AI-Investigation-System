//using System;
//using System.Collections.Generic;
//using System.Net.Http.Json;
//using System.Text;
//using System.Text.Json.Serialization;

//namespace PBM.Infrastructure.Tests
//{
//    public class GeminiTest
//    {
//        [Fact]
//        public async Task Test()
//        {
//            var apiKey = "AQ.Ab8RN6KjH2bcDTOH1ynVDZpyhlLHfSkDGiCF9-ZUlxZuCx3eNw";
//            var endpoint = new Uri("https://generativelanguage.googleapis.com/v1beta2");

//            // Instantiate the custom implementation mapping the Microsoft Framework interface to Gemini REST
//            var customGeminiClient = new GeminiChatClient(apiKey, "gemini-2.5-flash");

//            var pbmAgent = new ChatClientAgent(
//                chatClient: customGeminiClient,
//                name: "PbmManualAssistant",
//                instructions: "You are an expert in Pharmacy Benefit Management (PBM), You should only answer questions in the context of PBM. "
//            );

//            string query = "What is NDC?";
//            Console.WriteLine($"User: {query}\n");

//            var response = await pbmAgent.RunAsync(query);
//            Console.WriteLine($"Agent: {response}");
//        }
//    }


//}
