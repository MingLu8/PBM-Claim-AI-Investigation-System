namespace ApiGateway.Endpoints
{
    internal class SessionData
    {
        public string Id { get; set; }
        public long CreatedAt { get; set; }
        public long LastAccessedAt { get; set; }
        public string UserSub { get; set; }
    }
}