using System;

namespace Framework.API;

/// <summary>
/// Fluent API helper for validating API response content.
/// Supports JSON and XML responses with automatic format detection.
/// 
/// Usage:
/// <code>
/// var response = await apiClient.GetAsync("/api/users/1");
/// ResponseValidator
///     .FromResponse(response)
///     .Validate("status", "active")
///     .Validate("email", "user@example.com")
///     .Validate("id", 1)
///     .ValidateFieldExists("createdAt");
/// </code>
/// </summary>
public sealed class ResponseValidator
{
    private readonly IResponseValidator _validator;
    private static readonly Serilog.ILogger Logger = Serilog.Log.Logger;

    private ResponseValidator(IResponseValidator validator)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator), "Validator cannot be null.");
    }

    /// <summary>
    /// Creates a ResponseValidator from an HttpResponseMessage.
    /// Automatically detects response format (JSON or XML).
    /// </summary>
    /// <param name="response">The HTTP response message.</param>
    /// <returns>A fluent ResponseValidator builder.</returns>
    /// <exception cref="ArgumentNullException">Thrown if response is null.</exception>
    public static ResponseValidator FromResponse(HttpResponseMessage response)
    {
        if (response is null)
        {
            throw new ArgumentNullException(nameof(response), "HttpResponseMessage cannot be null.");
        }

        var contentType = response.Content?.Headers?.ContentType?.ToString();
        var content = response.Content?.ReadAsStringAsync().GetAwaiter().GetResult() ?? string.Empty;

        var validator = CreateValidator(content, contentType);
        return new ResponseValidator(validator);
    }

    /// <summary>
    /// Creates a ResponseValidator from raw response content.
    /// Automatically detects response format (JSON or XML).
    /// </summary>
    /// <param name="responseContent">The raw response body as a string.</param>
    /// <param name="contentType">The Content-Type header (optional; will auto-detect if null).</param>
    /// <returns>A fluent ResponseValidator builder.</returns>
    /// <exception cref="ArgumentException">Thrown if content is null or empty.</exception>
    public static ResponseValidator FromContent(string responseContent, string? contentType = null)
    {
        if (string.IsNullOrWhiteSpace(responseContent))
        {
            throw new ArgumentException("Response content cannot be null or empty.", nameof(responseContent));
        }

        var validator = CreateValidator(responseContent, contentType);
        return new ResponseValidator(validator);
    }

    /// <summary>
    /// Validates that a field exists and equals the expected value.
    /// Supports nested paths using dot notation (e.g., "user.email", "data.profile.age").
    /// </summary>
    /// <param name="fieldPath">The field path (can be nested with dots).</param>
    /// <param name="expectedValue">The expected value (string, int, bool, etc.).</param>
    /// <returns>This builder for fluent chaining.</returns>
    /// <exception cref="AssertionException">Thrown if validation fails.</exception>
    public ResponseValidator Validate(string fieldPath, object? expectedValue)
    {
        _validator.Validate(fieldPath, expectedValue);
        return this;
    }

    /// <summary>
    /// Validates that a field exists (regardless of its value).
    /// </summary>
    /// <param name="fieldPath">The field path (can be nested with dots).</param>
    /// <returns>This builder for fluent chaining.</returns>
    /// <exception cref="AssertionException">Thrown if field doesn't exist.</exception>
    public ResponseValidator ValidateFieldExists(string fieldPath)
    {
        _validator.ValidateFieldExists(fieldPath);
        return this;
    }

    /// <summary>
    /// Validates that a field does NOT exist.
    /// </summary>
    /// <param name="fieldPath">The field path (can be nested with dots).</param>
    /// <returns>This builder for fluent chaining.</returns>
    /// <exception cref="AssertionException">Thrown if field exists.</exception>
    public ResponseValidator ValidateFieldNotExists(string fieldPath)
    {
        _validator.ValidateFieldNotExists(fieldPath);
        return this;
    }

    /// <summary>
    /// Validates that a field is of a specific type.
    /// </summary>
    /// <param name="fieldPath">The field path (can be nested with dots).</param>
    /// <param name="expectedType">The expected type (typeof(string), typeof(int), etc.).</param>
    /// <returns>This builder for fluent chaining.</returns>
    /// <exception cref="AssertionException">Thrown if type doesn't match.</exception>
    public ResponseValidator ValidateType(string fieldPath, Type expectedType)
    {
        _validator.ValidateType(fieldPath, expectedType);
        return this;
    }

    /// <summary>
    /// Validates that a field's value is of a specific generic type.
    /// </summary>
    /// <typeparam name="T">The expected type.</typeparam>
    /// <param name="fieldPath">The field path (can be nested with dots).</param>
    /// <returns>This builder for fluent chaining.</returns>
    /// <exception cref="AssertionException">Thrown if type doesn't match.</exception>
    public ResponseValidator ValidateType<T>(string fieldPath)
    {
        _validator.ValidateType(fieldPath, typeof(T));
        return this;
    }

    /// <summary>
    /// Validates that a field's value contains a substring (case-insensitive).
    /// </summary>
    /// <param name="fieldPath">The field path (can be nested with dots).</param>
    /// <param name="expectedSubstring">The substring to find.</param>
    /// <returns>This builder for fluent chaining.</returns>
    /// <exception cref="AssertionException">Thrown if substring not found.</exception>
    public ResponseValidator ValidateContains(string fieldPath, string expectedSubstring)
    {
        _validator.ValidateContains(fieldPath, expectedSubstring);
        return this;
    }

    /// <summary>
    /// Gets the raw response content as a string.
    /// Useful for custom validation logic or debugging.
    /// </summary>
    /// <returns>The raw response content.</returns>
    public string GetRawContent() => _validator.GetRawContent();

    /// <summary>
    /// Gets the Content-Type of the response.
    /// </summary>
    /// <returns>The Content-Type header value.</returns>
    public string GetContentType() => _validator.GetContentType();

    /// <summary>
    /// Validates multiple fields at once using inline syntax.
    /// Example: ValidateMultiple(("status", "active"), ("id", 1))
    /// </summary>
    /// <param name="validations">Array of (fieldPath, expectedValue) tuples.</param>
    /// <returns>This builder for fluent chaining.</returns>
    /// <exception cref="AssertionException">Thrown if any validation fails.</exception>
    public ResponseValidator ValidateMultiple(params (string fieldPath, object? expectedValue)[] validations)
    {
        if (validations is null || validations.Length == 0)
        {
            throw new ArgumentException("At least one validation tuple must be provided.", nameof(validations));
        }

        foreach (var (fieldPath, expectedValue) in validations)
        {
            Validate(fieldPath, expectedValue);
        }

        return this;
    }

    /// <summary>
    /// Executes a custom assertion using the raw response content.
    /// Useful for complex validations that don't fit standard patterns.
    /// </summary>
    /// <param name="assertionFunc">Function that receives the validator and performs custom assertions.</param>
    /// <returns>This builder for fluent chaining.</returns>
    public ResponseValidator ValidateCustom(Action<IResponseValidator> assertionFunc)
    {
        if (assertionFunc is null)
        {
            throw new ArgumentNullException(nameof(assertionFunc), "Assertion function cannot be null.");
        }

        assertionFunc(_validator);
        return this;
    }

    private static IResponseValidator CreateValidator(string responseContent, string? contentType)
    {
        var mediaType = NormalizeContentType(contentType);
        var format = DetermineFormat(mediaType, responseContent);

        Logger.Debug("Creating {Format}ResponseValidator. Content-Type: {ContentType}", format, contentType ?? "auto-detected");

        return format switch
        {
            ResponseFormat.Json => new JsonResponseValidator(responseContent, contentType ?? "application/json", Logger),
            ResponseFormat.Xml => new XmlResponseValidator(responseContent, contentType ?? "application/xml", Logger),
            _ => throw new InvalidOperationException(
                $"Unable to determine response format. Content-Type: {contentType ?? "null"}. " +
                $"Response preview: {responseContent.Substring(0, Math.Min(100, responseContent.Length))}"),
        };
    }

    private static ResponseFormat DetermineFormat(string? normalizedContentType, string responseContent)
    {
        if (!string.IsNullOrWhiteSpace(normalizedContentType))
        {
            if (normalizedContentType.Contains("json", StringComparison.OrdinalIgnoreCase))
            {
                return ResponseFormat.Json;
            }

            if (normalizedContentType.Contains("xml", StringComparison.OrdinalIgnoreCase))
            {
                return ResponseFormat.Xml;
            }
        }

        var trimmedContent = responseContent.Trim();

        if (trimmedContent.StartsWith("{", StringComparison.Ordinal) ||
            trimmedContent.StartsWith("[", StringComparison.Ordinal))
        {
            Logger.Debug("Content-Type not provided; detected JSON format from payload.");
            return ResponseFormat.Json;
        }

        if (trimmedContent.StartsWith("<", StringComparison.Ordinal))
        {
            Logger.Debug("Content-Type not provided; detected XML format from payload.");
            return ResponseFormat.Xml;
        }

        Logger.Warning("Unable to determine response format from Content-Type or payload. Defaulting to JSON.");
        return ResponseFormat.Json;
    }

    private static string? NormalizeContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return null;
        }

        var parts = contentType.Split(';');
        return parts[0].Trim();
    }

    private enum ResponseFormat
    {
        Json,
        Xml,
    }
}
