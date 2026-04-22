using Newtonsoft.Json;
using System.Text.RegularExpressions;

namespace Framework.Data;

/// <summary>
/// Reads test data from JSON files and deserialises them into strongly-typed models with environment variable substitution.
/// Use <see cref="Read{T}"/> to load any JSON file by path; throws <see cref="FileNotFoundException"/> if the file does not exist.
/// Supports ${VARIABLE_NAME} syntax for replacing placeholders with environment variable values.
/// Example: "password": "${TEST_USER_PASSWORD}" will be replaced with the TEST_USER_PASSWORD environment variable.
/// </summary>
public static class JsonDataProvider
{
    /// <summary>
    /// Reads and deserializes a JSON file, replacing environment variable placeholders (${VAR_NAME}).
    /// </summary>
    /// <typeparam name="T">The type to deserialize the JSON into</typeparam>
    /// <param name="filePath">Path to the JSON file</param>
    /// <returns>Deserialized object with environment variables substituted</returns>
    /// <exception cref="FileNotFoundException">Thrown if the file does not exist</exception>
    /// <exception cref="InvalidOperationException">Thrown if a required environment variable is missing</exception>
    public static T? Read<T>(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"JSON file not found: {filePath}", filePath);
        }

        var content = File.ReadAllText(filePath);

        // Replace environment variable placeholders: ${VAR_NAME}
        content = SubstituteEnvironmentVariables(content);

        return JsonConvert.DeserializeObject<T>(content);
    }

    /// <summary>
    /// Replaces environment variable placeholders in the format ${VARIABLE_NAME} with their values.
    /// Supports optional fallback syntax: ${VAR_NAME:-defaultValue}
    /// If env var is not found and no fallback is provided, throws InvalidOperationException.
    /// </summary>
    /// <param name="content">The content string containing placeholders</param>
    /// <returns>Content with all placeholders replaced</returns>
    /// <exception cref="InvalidOperationException">Thrown if a required environment variable is missing</exception>
    private static string SubstituteEnvironmentVariables(string content)
    {
        // Pattern to match ${VARIABLE_NAME} or ${VARIABLE_NAME:-defaultValue}
        var pattern = @"\$\{([A-Za-z_][A-Za-z0-9_]*)(?::-(.*?))?\}";
        var regex = new Regex(pattern);

        return regex.Replace(content, match =>
        {
            var variableName = match.Groups[1].Value;
            var defaultValue = match.Groups[2].Success ? match.Groups[2].Value : null;
            var value = Environment.GetEnvironmentVariable(variableName);

            // If env var is set, use it
            if (!string.IsNullOrEmpty(value))
            {
                return value;
            }

            // If default value is provided (syntax: ${VAR:-default}), use it
            if (defaultValue != null)
            {
                return defaultValue;
            }

            // If neither env var nor default is available, throw error
            throw new InvalidOperationException(
                $"Environment variable '{variableName}' not found and no default value provided. " +
                $"Please set the environment variable '{variableName}' before running tests. " +
                "For role-based login data, configure TEST_<ROLE>_EMAIL and TEST_<ROLE>_PASSWORD " +
                "for the role(s) used by the tests you are running.");
        });
    }
}
