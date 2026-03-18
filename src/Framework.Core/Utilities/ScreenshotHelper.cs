using OpenQA.Selenium;

namespace Framework.Core.Utilities;

/// <summary>
/// Utility for capturing browser screenshots on test failure.
/// Saves the screenshot as a PNG file under a specified output directory with a timestamp and
/// GUID-suffixed filename to prevent collisions when multiple tests fail concurrently.
/// </summary>
public static class ScreenshotHelper
{
    public static string CaptureScreenshot(IWebDriver driver, string outputDirectory, string filePrefix = "screenshot")
    {
        Directory.CreateDirectory(outputDirectory);

        if (driver is not ITakesScreenshot takesScreenshot)
        {
            throw new InvalidOperationException("Current WebDriver does not support screenshots.");
        }

        var safePrefix = SanitizeForFileName(filePrefix);
        var fileName = $"{safePrefix}_{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid():N}.png";
        var fullPath = Path.Combine(outputDirectory, fileName);

        var screenshot = takesScreenshot.GetScreenshot();
        screenshot.SaveAsFile(fullPath);

        return fullPath;
    }

    private static string SanitizeForFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "screenshot";
        }

        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(value.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "screenshot" : sanitized;
    }
}
