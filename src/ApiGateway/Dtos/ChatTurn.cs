namespace ApiGateway.Dtos
{
    public record ChatTurn(string Role, string Content);

    public record SessionSummary(string Id, string Title, long LastAccessedAt, int MessageCount);
}
