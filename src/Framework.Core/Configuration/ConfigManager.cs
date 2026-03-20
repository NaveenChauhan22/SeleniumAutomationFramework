using Microsoft.Extensions.Configuration;

namespace Framework.Core.Configuration;

/// <summary>
/// Thin static wrapper around <see cref="Microsoft.Extensions.Configuration.IConfiguration"/>.
/// Loads settings from <c>appsettings.json</c>, an optional environment-specific overlay
/// (<c>appsettings.{TEST_ENV}.json</c>), and environment variables (highest priority).
/// Provides typed accessors — <see cref="GetString"/>, <see cref="GetInt"/>,
/// <see cref="GetBool"/> — that enforce required configuration keys with validation.
/// Throws <see cref="InvalidOperationException"/> if a required key is missing or invalid.
/// </summary>
public static class ConfigManager
{
    private static readonly Lazy<IConfigurationRoot> ConfigRoot = new(LoadConfiguration);

    public static string GetString(string key)
    {
        var value = ConfigRoot.Value[key];
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"Missing required config key: {key}");
        return value;
    }

    public static int GetInt(string key)
    {
        var value = ConfigRoot.Value[key];
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"Missing required config key: {key}");

        if (int.TryParse(value, out var parsed))
            return parsed;

        throw new InvalidOperationException($"Config key '{key}' has invalid int value: {value}");
    }

    public static bool GetBool(string key)
    {
        var value = ConfigRoot.Value[key];
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"Missing required config key: {key}");

        if (bool.TryParse(value, out var parsed))
            return parsed;

        throw new InvalidOperationException($"Config key '{key}' has invalid bool value: {value}");
    }

    private static IConfigurationRoot LoadConfiguration()
    {
        var environment = Environment.GetEnvironmentVariable("TEST_ENV") ?? "local";

        return new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();
    }
}