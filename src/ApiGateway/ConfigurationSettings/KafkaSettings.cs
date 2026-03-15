namespace ApiGateway.ConfigurationSettings
{
    public class KafkaSettings
    {
        public string BootstrapServers { get; set; } = "";
        public string RequestTopic { get; set; } = "";
        public int TimeoutSeconds { get; set; }
    }

}
