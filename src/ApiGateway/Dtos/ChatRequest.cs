using System.ComponentModel;

namespace ApiGateway.Dtos;

public class ChatRequest
{
    [property: DefaultValue("Analyze this raw NCPDP claim: 302-C2^~")]
    public string Prompt { get; set; }
}
