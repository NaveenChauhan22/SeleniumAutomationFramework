using Newtonsoft.Json.Linq;
using NUnit.Framework;
using System;

namespace Framework.API;

/// <summary>
/// JSON response validator for asserting key-value pairs in JSON API responses.
/// Supports nested paths via dot notation (e.g., "data.user.email").
/// </summary>
public sealed class JsonResponseValidator : IResponseValidator
{
    private readonly string _rawContent;
    private readonly string _contentType;
    private readonly JToken? _rootToken;
    private readonly Serilog.ILogger _logger;

    public JsonResponseValidator(string responseContent, string contentType, Serilog.ILogger? logger = null)
    {
        if (string.IsNullOrWhiteSpace(responseContent))
        {
            throw new ArgumentException("Response content cannot be null or empty.", nameof(responseContent));
        }

        _rawContent = responseContent;
        _contentType = contentType ?? "application/json";
        _logger = logger ?? Serilog.Log.Logger;

        try
        {
            _rootToken = JToken.Parse(responseContent);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to parse response as JSON. Response: {responseContent.Substring(0, Math.Min(100, responseContent.Length))}",
                ex);
        }
    }

    public IResponseValidator Validate(string fieldPath, object? expectedValue)
    {
        if (string.IsNullOrWhiteSpace(fieldPath))
        {
            throw new ArgumentException("Field path cannot be null or empty.", nameof(fieldPath));
        }

        var token = SelectToken(fieldPath);

        if (token is null)
        {
            throw new AssertionException(
                $"Field '{fieldPath}' not found in JSON response. " +
                $"Expected: {expectedValue ?? "null"}. " +
                $"Response: {_rawContent.Substring(0, Math.Min(200, _rawContent.Length))}");
        }

        var actualValue = token.ToObject<object>();

        if (!ValuesEqual(actualValue, expectedValue))
        {
            throw new AssertionException(
                $"Field '{fieldPath}' value mismatch. " +
                $"Expected: {FormatValue(expectedValue)}, " +
                $"Actual: {FormatValue(actualValue)}");
        }

        _logger.Debug("JSON validation passed: {FieldPath} = {ExpectedValue}", fieldPath, expectedValue);
        return this;
    }

    public IResponseValidator ValidateFieldExists(string fieldPath)
    {
        if (string.IsNullOrWhiteSpace(fieldPath))
        {
            throw new ArgumentException("Field path cannot be null or empty.", nameof(fieldPath));
        }

        var token = SelectToken(fieldPath);

        if (token is null)
        {
            throw new AssertionException(
                $"Field '{fieldPath}' does not exist in JSON response. " +
                $"Response: {_rawContent.Substring(0, Math.Min(200, _rawContent.Length))}");
        }

        _logger.Debug("JSON field exists validation passed: {FieldPath}", fieldPath);
        return this;
    }

    public IResponseValidator ValidateFieldNotExists(string fieldPath)
    {
        if (string.IsNullOrWhiteSpace(fieldPath))
        {
            throw new ArgumentException("Field path cannot be null or empty.", nameof(fieldPath));
        }

        var token = SelectToken(fieldPath);

        if (token is not null)
        {
            throw new AssertionException(
                $"Field '{fieldPath}' should not exist in JSON response, but found value: {FormatValue(token.ToObject<object>())}");
        }

        _logger.Debug("JSON field not exists validation passed: {FieldPath}", fieldPath);
        return this;
    }

    public IResponseValidator ValidateType(string fieldPath, Type expectedType)
    {
        if (string.IsNullOrWhiteSpace(fieldPath))
        {
            throw new ArgumentException("Field path cannot be null or empty.", nameof(fieldPath));
        }

        if (expectedType is null)
        {
            throw new ArgumentNullException(nameof(expectedType), "Expected type cannot be null.");
        }

        var token = SelectToken(fieldPath);

        if (token is null)
        {
            throw new AssertionException(
                $"Field '{fieldPath}' not found in JSON response.");
        }

        var actualValue = token.ToObject<object>();
        var actualType = actualValue?.GetType();

        if (actualType != expectedType && !IsCompatibleNumericType(actualValue, expectedType))
        {
            throw new AssertionException(
                $"Field '{fieldPath}' type mismatch. " +
                $"Expected: {expectedType.Name}, " +
                $"Actual: {actualType?.Name ?? "null"}");
        }

        _logger.Debug("JSON type validation passed: {FieldPath} is {ExpectedType}", fieldPath, expectedType.Name);
        return this;
    }

    public IResponseValidator ValidateContains(string fieldPath, string expectedSubstring)
    {
        if (string.IsNullOrWhiteSpace(fieldPath))
        {
            throw new ArgumentException("Field path cannot be null or empty.", nameof(fieldPath));
        }

        if (expectedSubstring is null)
        {
            throw new ArgumentNullException(nameof(expectedSubstring), "Expected substring cannot be null.");
        }

        var token = SelectToken(fieldPath);

        if (token is null)
        {
            throw new AssertionException(
                $"Field '{fieldPath}' not found in JSON response.");
        }

        var actualValue = token.ToObject<object>()?.ToString() ?? string.Empty;

        if (!actualValue.Contains(expectedSubstring, StringComparison.OrdinalIgnoreCase))
        {
            throw new AssertionException(
                $"Field '{fieldPath}' does not contain expected substring. " +
                $"Expected substring: '{expectedSubstring}', " +
                $"Actual: '{actualValue}'");
        }

        _logger.Debug("JSON contains validation passed: {FieldPath} contains '{ExpectedSubstring}'", fieldPath, expectedSubstring);
        return this;
    }

    public string GetRawContent() => _rawContent;

    public string GetContentType() => _contentType;

    /// <summary>
    /// Selects a token using dot-notation path.
    /// Supports nested paths like "data.user.email".
    /// </summary>
    private JToken? SelectToken(string path)
    {
        if (_rootToken is null)
        {
            return null;
        }

        try
        {
            // Try JSONPath notation first (e.g., "$.data.user.email")
            var pathWithPrefix = path.StartsWith("$") ? path : $"$.{path}";
            return _rootToken.SelectToken(pathWithPrefix);
        }
        catch (Exception ex)
        {
            _logger.Warning("Failed to select token at path '{Path}': {Message}", path, ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Compares two values, handling type coercion.
    /// </summary>
    private static bool ValuesEqual(object? actual, object? expected)
    {
        if (actual is null && expected is null)
        {
            return true;
        }

        if (actual is null || expected is null)
        {
            return false;
        }

        // Direct equality
        if (actual.Equals(expected))
        {
            return true;
        }

        // Try numeric comparison (e.g., 1 vs "1")
        if (int.TryParse(actual.ToString(), out var actualInt) &&
            int.TryParse(expected.ToString(), out var expectedInt))
        {
            return actualInt == expectedInt;
        }

        // String comparison
        return actual.ToString() == expected.ToString();
    }

    private static bool IsCompatibleNumericType(object? actualValue, Type expectedType)
    {
        if (actualValue is null)
        {
            return false;
        }

        var actualType = actualValue.GetType();
        if (!IsNumericType(actualType) || !IsNumericType(expectedType))
        {
            return false;
        }

        try
        {
            _ = Convert.ChangeType(actualValue, expectedType);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsNumericType(Type type)
    {
        return Type.GetTypeCode(type) switch
        {
            TypeCode.Byte => true,
            TypeCode.SByte => true,
            TypeCode.UInt16 => true,
            TypeCode.UInt32 => true,
            TypeCode.UInt64 => true,
            TypeCode.Int16 => true,
            TypeCode.Int32 => true,
            TypeCode.Int64 => true,
            TypeCode.Decimal => true,
            TypeCode.Double => true,
            TypeCode.Single => true,
            _ => false
        };
    }

    /// <summary>
    /// Formats a value for display in error messages.
    /// </summary>
    private static string FormatValue(object? value)
    {
        if (value is null)
        {
            return "<null>";
        }

        if (value is string str)
        {
            return $"\"{str}\"";
        }

        if (value is bool b)
        {
            return b.ToString().ToLowerInvariant();
        }

        return value.ToString() ?? "<unknown>";
    }
}
