using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace ApiGateway.Plugins;

public class PharmacyNpiParserPlugin
{
    [KernelFunction("extract_npi_from_ncpdp")]
    [Description("Parses a raw NCPDP D0 claim string to extract the 10-digit Pharmacy NPI.")]
    public string ExtractPharmacyNpi(
        [Description("The raw NCPDP telecom transaction string")] string ncpdpPayload)
    {
        // Zero-allocation parsing
        ReadOnlySpan<char> payloadSpan = ncpdpPayload.AsSpan();

        // Example: Look for the Pharmacy Provider segment identifier (e.g., "201-B1")
        ReadOnlySpan<char> segmentIdentifier = "201-B1".AsSpan();
        int index = payloadSpan.IndexOf(segmentIdentifier);

        if (index != -1)
        {
            // Slice the span to grab the 10-digit NPI immediately following the identifier
            // We only allocate a new string at the very end when returning the specific data
            ReadOnlySpan<char> npiSpan = payloadSpan.Slice(index + segmentIdentifier.Length, 10);
            return npiSpan.ToString();
        }

        return "NPI not found in payload.";
    }
}
