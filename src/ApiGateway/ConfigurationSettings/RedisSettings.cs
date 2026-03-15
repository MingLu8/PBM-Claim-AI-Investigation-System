namespace ApiGateway.ConfigurationSettings
{
    public class RedisSettings
    {
        public string ConnectionString { get; set; } = "";
        public string RequestTopic { get; set; } = "";
        public string ResponseChannel { get; set; } = "";
        public long? StreamLimit { get; internal set; }
    }

}
