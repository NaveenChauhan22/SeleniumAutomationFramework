using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Framework.API;

/// <summary>
/// Sanitizes API request/response dumps by masking sensitive header/body values.
/// Keys <c>email</c> and <c>password</c> are always masked in the body.
/// Keys <c>token</c> and <c>authorization</c> are masked in the body only when
/// <paramref name="showBearerToken"/> is <c>false</c>.
/// The <c>Authorization</c> request header is masked only when <paramref name="showBearerToken"/> is <c>false</c>.
/// </summary>
public static class APIContentSanitizer
{
    public const string HiddenBearerMessage = "Bearer Token is being hidden for security purpose";

    // Always replaced regardless of showBearerToken.
    private static readonly Dictionary<string, string> AlwaysMaskedBodyKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["email"] = "${TEST_USER_EMAIL}",
        ["password"] = "${TEST_USER_PASSWORD}"
    };

    // Replaced only when showBearerToken is false.
    private static readonly Dictionary<string, string> BearerMaskedBodyKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["token"] = "${AUTH_BEARER_TOKEN}",
        ["authorization"] = HiddenBearerMessage
    };

    /// <summary>
    /// Sanitizes a formatted request or response dump string.
    /// </summary>
    public static string SanitizeDump(string dump, bool showBearerToken)
    {
        var splitMarker = Environment.NewLine + Environment.NewLine;
        var markerIndex = dump.IndexOf(splitMarker, StringComparison.Ordinal);

        var headerPart = markerIndex >= 0 ? dump[..markerIndex] : dump;
        var bodyPart = markerIndex >= 0 ? dump[(markerIndex + splitMarker.Length)..] : string.Empty;

        var sanitizedHeaders = SanitizeHeaders(headerPart, showBearerToken);
        var sanitizedBody = SanitizeBody(bodyPart, showBearerToken);

        if (markerIndex < 0)
        {
            return sanitizedHeaders;
        }

        return sanitizedHeaders + splitMarker + sanitizedBody;
    }

    /// <summary>
    /// Sanitizes a string (response body, error message, or log content) by masking sensitive values.
    /// Safely handles both JSON and plain text strings. Always hides bearer tokens and sensitive fields.
    /// </summary>
    /// <param name="content">The string content to sanitize (can be JSON, plain text, or null).</param>
    /// <returns>Sanitized content with sensitive values masked. Returns empty string if input is null.</returns>
    public static string SanitizeContent(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return content ?? string.Empty;

        // Always sanitize by treating as a body (which handles JSON if present)
        return SanitizeBody(content, showBearerToken: false);
    }

    private static string SanitizeHeaders(string headers, bool showBearerToken)
    {
        if (showBearerToken)
        {
            return headers;
        }

        var lines = headers.Split(Environment.NewLine).ToList();

        for (var i = 0; i < lines.Count; i++)
        {
            if (lines[i].StartsWith("Authorization:", StringComparison.OrdinalIgnoreCase))
            {
                lines[i] = $"Authorization: {HiddenBearerMessage}";
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string SanitizeBody(string body, bool showBearerToken)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return body;
        }

        try
        {
            var token = JToken.Parse(body);
            ReplaceSensitiveProperties(token, showBearerToken);
            return token.ToString(Formatting.Indented);
        }
        catch
        {
            return body;
        }
    }

    private static void ReplaceSensitiveProperties(JToken token, bool showBearerToken)
    {
        if (token is JObject obj)
        {
            foreach (var property in obj.Properties().ToList())
            {
                if (AlwaysMaskedBodyKeys.TryGetValue(property.Name, out var alwaysReplacement))
                {
                    property.Value = alwaysReplacement;
                    continue;
                }

                if (!showBearerToken && BearerMaskedBodyKeys.TryGetValue(property.Name, out var bearerReplacement))
                {
                    property.Value = bearerReplacement;
                    continue;
                }

                ReplaceSensitiveProperties(property.Value, showBearerToken);
            }

            return;
        }

        if (token is JArray arr)
        {
            foreach (var item in arr)
            {
                ReplaceSensitiveProperties(item, showBearerToken);
            }
        }
    }
}
