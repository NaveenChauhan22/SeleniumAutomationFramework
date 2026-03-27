using System;

namespace Framework.API;

/// <summary>
/// Defines the contract for response validators that assert key-value pairs in API responses.
/// Implementations support different content formats (JSON, XML, etc.).
/// </summary>
public interface IResponseValidator
{
    /// <summary>
    /// Validates that a field exists at the given path and equals the expected value.
    /// </summary>
    /// <param name="fieldPath">Dot-separated path to the field (e.g., "user.email", "status").</param>
    /// <param name="expectedValue">The expected value (string, int, bool, etc.).</param>
    /// <returns>This validator for fluent chaining.</returns>
    /// <exception cref="AssertionException">Thrown when field is not found or value doesn't match.</exception>
    IResponseValidator Validate(string fieldPath, object? expectedValue);

    /// <summary>
    /// Validates that a field exists at the given path (regardless of value).
    /// </summary>
    /// <param name="fieldPath">Dot-separated path to the field.</param>
    /// <returns>This validator for fluent chaining.</returns>
    /// <exception cref="AssertionException">Thrown when field is not found.</exception>
    IResponseValidator ValidateFieldExists(string fieldPath);

    /// <summary>
    /// Validates that a field does NOT exist at the given path.
    /// </summary>
    /// <param name="fieldPath">Dot-separated path to the field.</param>
    /// <returns>This validator for fluent chaining.</returns>
    /// <exception cref="AssertionException">Thrown when field exists.</exception>
    IResponseValidator ValidateFieldNotExists(string fieldPath);

    /// <summary>
    /// Validates that a field's value is of a specific type.
    /// </summary>
    /// <param name="fieldPath">Dot-separated path to the field.</param>
    /// <param name="expectedType">The expected type (e.g., typeof(string), typeof(int)).</param>
    /// <returns>This validator for fluent chaining.</returns>
    /// <exception cref="AssertionException">Thrown when field type doesn't match.</exception>
    IResponseValidator ValidateType(string fieldPath, Type expectedType);

    /// <summary>
    /// Validates that a field's value contains a substring.
    /// </summary>
    /// <param name="fieldPath">Dot-separated path to the field.</param>
    /// <param name="expectedSubstring">The substring to find.</param>
    /// <returns>This validator for fluent chaining.</returns>
    /// <exception cref="AssertionException">Thrown when substring is not found.</exception>
    IResponseValidator ValidateContains(string fieldPath, string expectedSubstring);

    /// <summary>
    /// Gets the raw response content as a string.
    /// Useful for custom assertions or debugging.
    /// </summary>
    string GetRawContent();

    /// <summary>
    /// Gets the Content-Type of the response.
    /// </summary>
    string GetContentType();
}
