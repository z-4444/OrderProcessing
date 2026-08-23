namespace OrderProcessing.Api.Concurrency;

public static class ConcurrencyHeader
{
    public static string ToETag(string token) => $"\"{token}\"";

    public static string? ParseIfMatch(string? ifMatch)
    {
        if (string.IsNullOrWhiteSpace(ifMatch) || ifMatch == "*")
        {
            return null;
        }

        // Support single ETag: "token" or W/"token"
        var value = ifMatch.Trim();
        if (value.StartsWith("W/", StringComparison.OrdinalIgnoreCase))
        {
            value = value[2..].Trim();
        }

        return value.Trim('"');
    }

    public static string? Resolve(
        string? ifMatchHeader,
        string? bodyToken)
    {
        var fromHeader = ParseIfMatch(ifMatchHeader);
        return !string.IsNullOrWhiteSpace(fromHeader) ? fromHeader : bodyToken?.Trim();
    }
}
