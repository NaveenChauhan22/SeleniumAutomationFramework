# Cross-Browser Execution Guide

This framework supports **Chrome**, **Edge**, and **Firefox** with a priority-based configuration system.

## Default Browser

The default browser is **Chrome** as configured in `config/appsettings.json`:

```json
"TestSettings": {
  "Browser": "chrome"
}
```

## Running with Different Browsers

### Method 1: Via Environment Variables (Highest Priority)

Override the browser at runtime using environment variables. This is the recommended approach for CI/CD pipelines and local testing.

#### Chrome
```powershell
$env:TestSettings__Browser = "chrome"; dotnet test tests/UITests/UITests.csproj
```

#### Edge
```powershell
$env:TestSettings__Browser = "edge"; dotnet test tests/UITests/UITests.csproj
```

#### Firefox
```powershell
$env:TestSettings__Browser = "firefox"; dotnet test tests/UITests/UITests.csproj
```

### Method 2: Via Config File

Edit `config/appsettings.json` and change the `Browser` value:

```json
"TestSettings": {
  "Browser": "edge"
}
```

Valid values: `chrome`, `edge`, `firefox`

## Configuration Priority

The driver initialization follows this priority order:

1. **Environment Variable** `TestSettings__Browser` (highest priority)
2. **Config File** `config/appsettings.json` → `TestSettings.Browser`
3. **Default** `chrome` (fallback)

## Example: Running Tests in All Browsers

### PowerShell

```powershell
# Chrome
dotnet test tests/UITests/UITests.csproj

# Edge
$env:TestSettings__Browser = "edge"; dotnet test tests/UITests/UITests.csproj

# Firefox
$env:TestSettings__Browser = "firefox"; dotnet test tests/UITests/UITests.csproj
```

### Batch/CMD

```batch
REM Chrome
dotnet test tests/UITests/UITests.csproj

REM Edge
set TestSettings__Browser=edge
dotnet test tests/UITests/UITests.csproj

REM Firefox
set TestSettings__Browser=firefox
dotnet test tests/UITests/UITests.csproj
```

## Test Execution Logging

When a test runs, the actual browser being used is logged:

**Console Output:**
```
STEP: Browser: chrome
STEP: Navigated to https://eventhub.rahulshettyacademy.com/login
```

**HTML Report:**
The test execution report (`reports/TestExecutionReport.html`) includes the browser information in the test logs.

## Technical Details

### BaseTest Configuration

- `ConfiguredBrowser` property reads from config and defaults to `chrome`
- `SetUp()` logs actual browser (including any environment variable override)
- Browser override via environment variable takes precedence over config file

### DriverManager

The `DriverManager.InitializeDriver()` method:
1. Checks `TestSettings__Browser` environment variable first
2. Falls back to `TestSettings:Browser` from `appsettings.json`
3. Uses `chrome` as final fallback
4. Initializes driver with proper options and timeouts
5. Sets implicit wait to 0 (explicit waits used instead)

### Supported Options per Browser

All browsers support these configuration options:

- **Headless Mode**: Set `TestSettings__Headless` env var or `TestSettings.Headless` in config
- **Page Load Timeout**: `TestSettings:PageLoadTimeoutSeconds` (default: 60s)
- **Script Timeout**: `TestSettings:ScriptTimeoutSeconds` (default: 30s)
- **Explicit Wait Timeout**: `TestSettings:ExplicitWaitSeconds` (default: 15s)

## Troubleshooting

### Test runs in wrong browser

1. Verify environment variable is set: `$env:TestSettings__Browser` (PowerShell)
2. Check `config/appsettings.json` for the fallback value
3. Confirm the driver is installed (WebDriverManager handles this automatically)

### Browser driver not found

WebDriverManager automatically downloads compatible drivers. Ensure:
- .NET SDK is installed
- Internet connection available (for first-time driver download)
- System has write permissions in temp folders
