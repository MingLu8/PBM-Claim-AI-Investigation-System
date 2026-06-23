//using Microsoft.Extensions.Caching.Distributed;
//using Microsoft.SemanticKernel.ChatCompletion;
//using System.Text.Json;
//namespace ApiGateway;

//public class DistributedInvestigationState(IDistributedCache cache)
//{
//    // C# 14 primary constructor handles the DI injection of IDistributedCache (e.g., Redis)

//    public async Task SaveStateAsync(string investigationId, ChatHistory history, CancellationToken ct = default)
//    {
//        // Serialize the SK ChatHistory object
//        var jsonState = JsonSerializer.Serialize(history);

//        // Save to Redis with an expiration (e.g., 24 hours for an investigation SLA)
//        var options = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24) };
//        await cache.SetStringAsync($"investigation:{investigationId}", jsonState, options, ct);
//    }

//    public async Task<ChatHistory> LoadStateAsync(string investigationId, CancellationToken ct = default)
//    {
//        var jsonState = await cache.GetStringAsync($"investigation:{investigationId}", ct);

//        if (string.IsNullOrEmpty(jsonState))
//        {
//            // Return a fresh state if none exists
//            return new ChatHistory("You are a strict PBM Fraud, Waste, and Abuse investigator...");
//        }

//        return JsonSerializer.Deserialize<ChatHistory>(jsonState) ?? new ChatHistory();
//    }
//}