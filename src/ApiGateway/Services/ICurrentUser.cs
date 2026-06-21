namespace ApiGateway.Services
{
    public interface ICurrentUser
    {
        bool IsAuthenticated { get; }
        string? Sub { get; }
        string Name { get; }
        string Email { get; }
        string UserId { get; }
        UserGroups Groups { get; }
    }
}