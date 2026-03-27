using NUnit.Framework;
using System;
using System.Xml.Linq;
using System.Xml.XPath;

namespace Framework.API;

/// <summary>
/// XML response validator for asserting key-value pairs in XML API responses.
/// Supports nested paths via simplified XPath notation (e.g., "data/user/email" or "data.user.email").
/// </summary>
public sealed class XmlResponseValidator : IResponseValidator
{
    private readonly string _rawContent;
    private readonly string _contentType;
    private readonly XDocument? _document;
    private readonly Serilog.ILogger _logger;

    public XmlResponseValidator(string responseContent, string contentType, Serilog.ILogger? logger = null)
    {
        if (string.IsNullOrWhiteSpace(responseContent))
        {
            throw new ArgumentException("Response content cannot be null or empty.", nameof(responseContent));
        }

        _rawContent = responseContent;
        _contentType = contentType ?? "application/xml";
        _logger = logger ?? Serilog.Log.Logger;

        try
        {
            _document = XDocument.Parse(responseContent);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to parse response as XML. Response: {responseContent.Substring(0, Math.Min(100, responseContent.Length))}",
                ex);
        }
    }

    public IResponseValidator Validate(string fieldPath, object? expectedValue)
    {
        if (string.IsNullOrWhiteSpace(fieldPath))
        {
            throw new ArgumentException("Field path cannot be null or empty.", nameof(fieldPath));
        }

        var element = SelectElement(fieldPath);

        if (element is null)
        {
            throw new AssertionException(
                $"Element '{fieldPath}' not found in XML response. " +
                $"Expected: {expectedValue ?? "null"}. " +
                $"Response: {_rawContent.Substring(0, Math.Min(200, _rawContent.Length))}");
        }

        var actualValue = GetElementValue(element);

        if (!ValuesEqual(actualValue, expectedValue))
        {
            throw new AssertionException(
                $"Element '{fieldPath}' value mismatch. " +
                $"Expected: {FormatValue(expectedValue)}, " +
                $"Actual: {FormatValue(actualValue)}");
        }

        _logger.Debug("XML validation passed: {FieldPath} = {ExpectedValue}", fieldPath, expectedValue);
        return this;
    }

    public IResponseValidator ValidateFieldExists(string fieldPath)
    {
        if (string.IsNullOrWhiteSpace(fieldPath))
        {
            throw new ArgumentException("Field path cannot be null or empty.", nameof(fieldPath));
        }

        var element = SelectElement(fieldPath);

        if (element is null)
        {
            throw new AssertionException(
                $"Element '{fieldPath}' does not exist in XML response. " +
                $"Response: {_rawContent.Substring(0, Math.Min(200, _rawContent.Length))}");
        }

        _logger.Debug("XML field exists validation passed: {FieldPath}", fieldPath);
        return this;
    }

    public IResponseValidator ValidateFieldNotExists(string fieldPath)
    {
        if (string.IsNullOrWhiteSpace(fieldPath))
        {
            throw new ArgumentException("Field path cannot be null or empty.", nameof(fieldPath));
        }

        var element = SelectElement(fieldPath);

        if (element is not null)
        {
            throw new AssertionException(
                $"Element '{fieldPath}' should not exist in XML response, but found value: {FormatValue(GetElementValue(element))}");
        }

        _logger.Debug("XML field not exists validation passed: {FieldPath}", fieldPath);
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

        var element = SelectElement(fieldPath);

        if (element is null)
        {
            throw new AssertionException(
                $"Element '{fieldPath}' not found in XML response.");
        }

        var actualValue = GetElementValue(element);
        var actualType = actualValue?.GetType();

        if (actualType != expectedType)
        {
            throw new AssertionException(
                $"Element '{fieldPath}' type mismatch. " +
                $"Expected: {expectedType.Name}, " +
                $"Actual: {actualType?.Name ?? "null"}");
        }

        _logger.Debug("XML type validation passed: {FieldPath} is {ExpectedType}", fieldPath, expectedType.Name);
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

        var element = SelectElement(fieldPath);

        if (element is null)
        {
            throw new AssertionException(
                $"Element '{fieldPath}' not found in XML response.");
        }

        var actualValue = GetElementValue(element)?.ToString() ?? string.Empty;

        if (!actualValue.Contains(expectedSubstring, StringComparison.OrdinalIgnoreCase))
        {
            throw new AssertionException(
                $"Element '{fieldPath}' does not contain expected substring. " +
                $"Expected substring: '{expectedSubstring}', " +
                $"Actual: '{actualValue}'");
        }

        _logger.Debug("XML contains validation passed: {FieldPath} contains '{ExpectedSubstring}'", fieldPath, expectedSubstring);
        return this;
    }

    public string GetRawContent() => _rawContent;

    public string GetContentType() => _contentType;

    /// <summary>
    /// Selects an element using simplified path notation.
    /// Supports both "data.user.email" and "data/user/email" formats.
    /// </summary>
    private XElement? SelectElement(string path)
    {
        if (_document?.Root is null)
        {
            return null;
        }

        try
        {
            // Normalize path: convert dots to slashes and construct XPath
            var normalizedPath = path.Replace(".", "/").Trim('/');
            var xpathQuery = $"//{normalizedPath[0].ToString().ToUpper()}{normalizedPath.Substring(1)}";

            // Try direct descendant XPath first
            var elements = _document.XPathSelectElements($".//{normalizedPath}");
            return elements.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.Warning("Failed to select element at path '{Path}': {Message}", path, ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Gets the value of an element.
    /// Returns the element's text content or attribute values as applicable.
    /// </summary>
    private static object? GetElementValue(XElement element)
    {
        if (element is null)
        {
            return null;
        }

        // Try to parse as number first
        if (int.TryParse(element.Value, out var intValue))
        {
            return intValue;
        }

        if (double.TryParse(element.Value, out var doubleValue))
        {
            return doubleValue;
        }

        // Try boolean
        if (bool.TryParse(element.Value, out var boolValue))
        {
            return boolValue;
        }

        // Return as string
        return element.Value;
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
