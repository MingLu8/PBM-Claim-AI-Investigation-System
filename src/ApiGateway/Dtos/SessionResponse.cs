namespace ApiGateway.Dtos
{
    public class SessionResponse
    {
        public string SessionId { get; }

        public SessionResponse(string sessionId)
        {
            SessionId = sessionId;
        }
    }
}