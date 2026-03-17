using System.ComponentModel;

namespace ApiGateway.Plugins;

public class CardHolderIdParserPlugin
{
    [KernelFunction("extract_cardholder_id")]
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

        // NCPDP fields are usually delimited by a group separator (GS), field separator (FS), 
        // or segment separator. For this example, we'll assume a standard caret '^' or segment terminator '~'
        int endIndex = remainingSpan.IndexOfAny('^', '~');

        if (endIndex == -1)
        {
            // If no delimiter is found, assume it runs to the end of the string
            return remainingSpan.ToString();
        }

        // Slice out exactly the ID and allocate a string only for the final result
        return remainingSpan.Slice(0, endIndex).ToString();
    }
}