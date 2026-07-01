namespace ApiGateway.ChatClients;

/// <summary>
/// Agent persona, bound from the "Agent" config section. Instructions are stored as a line
/// array (JSON has no multi-line string) and joined with '\n' at build time. Use the token
/// "{memoryTool}" anywhere in the text; it is replaced with the memory tool's actual name.
/// </summary>
public class AgentSettings
{
    public string Name { get; init; } = "PharmacyParserAgent";
    public string[] Instructions { get; init; } = [];
}
