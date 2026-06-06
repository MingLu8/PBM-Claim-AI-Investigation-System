using System;
using System.ComponentModel;

namespace ApiGateway.Plugins;

public class CardHolderIdParser
{
    [Description("Parses a raw NCPDP D0 claim string to extract the Member/Cardholder ID.")]
    public string ExtractCardholderId(
        [Description("The raw NCPDP telecom transaction string")] string ncpdpPayload)
    {
        ReadOnlySpan<char> payloadSpan = ncpdpPayload.AsSpan();

        // Field identifier for Cardholder ID in NCPDP D0
        ReadOnlySpan<char> fieldIdentifier = "302-C2".AsSpan();
        int startIndex = payloadSpan.IndexOf(fieldIdentifier);

        if (startIndex == -1)
        {
            return "Cardholder ID not found in payload.";
        }

        // Move the index past the identifier to the start of the actual ID value
        startIndex += fieldIdentifier.Length;
        ReadOnlySpan<char> remainingSpan = payloadSpan.Slice(startIndex);

        // Standard segment/field separators in NCPDP
        int endIndex = remainingSpan.IndexOfAny('^', '~');

        if (endIndex == -1)
        {
            return remainingSpan.ToString();
        }

        // Keep allocation zero-copy until the final string return
        return remainingSpan.Slice(0, endIndex).ToString();
    }
}