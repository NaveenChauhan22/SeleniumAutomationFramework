using Microsoft.Extensions.Configuration;

namespace Framework.Core.Configuration;

/// <summary>
/// Thin static wrapper around <see cref="Microsoft.Extensions.Configuration.IConfiguration"/>.
/// Loads settings from <c>appsettings.json</c>, an optional environment-specific overlay
/// (<c>appsettings.{TEST_ENV}.json</c>), and environment variables (highest priority).
/// Provides typed accessors — <see cref="GetString"/>, <see cref="GetInt"/>,
/// <see cref="GetBool"/> — with sensible defaults so tests never throw on a missing key.
/// </summary>
public static class ConfigManager
{
    private static readonly Lazy<IConfigurationRoot> ConfigRoot = new(LoadConfiguration);

    public static string GetString(string key, string defaultValue = "")
    {
        var value = ConfigRoot.Value[key];
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
    }

    public static int GetInt(string key, int defaultValue)
    {
        var value = ConfigRoot.Value[key];
        return int.TryParse(value, out var parsed) ? parsed : defaultValue;
    }

    public static bool GetBool(string key, bool defaultValue = false)
    {
        var value = ConfigRoot.Value[key];
        return bool.TryParse(value, out var parsed) ? parsed : defaultValue;
    }

    private static IConfigurationRoot LoadConfiguration()
    {
        var environment = Environment.GetEnvironmentVariable("TEST_ENV") ?? "local";

        return new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();
    }
}