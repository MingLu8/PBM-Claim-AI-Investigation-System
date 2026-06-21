namespace ApiGateway.Dtos
{
    public record SessionData(string Id, long CreatedAt, long LastAccessAt, string? User, IEnumerable<ChatTurn> History);
}
