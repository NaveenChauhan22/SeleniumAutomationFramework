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

### Method 1: Via .env File (Recommended for Local Development)

Add browser configuration to your `.env` file:

```bash
# .env
TEST_USER_EMAIL=correctemail@example.com
TEST_USER_PASSWORD=correctpassword

# Browser Configuration (optional - uncomment to override default)
TestSettings__Browser=edge
TestSettings__Headless=false
```

Then run tests normally:
```powershell
dotnet test tests/UITests/UITests.csproj
```

The .env file is automatically loaded by the framework before tests execute (via ModuleInitializer in AllureHooks).

### Method 2: Via Environment Variables (Best for CI/CD)

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

### Method 3: Via Config File

Edit `config/appsettings.json` and change the `Browser` value:

```json
"TestSettings": {
  "Browser": "edge"
}
```

Valid values: `chrome`, `edge`, `firefox`

## Headless Mode

By default, the browser runs in normal (non-headless) mode so you can see the tests executing. To run in headless mode (no UI, faster execution):

### Headless via .env File
```bash
# .env
TestSettings__Headless=true
```

### Headless via Environment Variable
```powershell
$env:TestSettings__Headless = "true"; dotnet test tests/UITests/UITests.csproj
```

### Headless via Config File
```json
"TestSettings": {
  "Headless": true
}
```

## Configuration Priority

The driver initialization follows this priority order (highest to lowest):

1. **Environment Variables** `TestSettings__Browser`, `TestSettings__Headless` (highest priority)
2. **.env File** (auto-loaded before tests, if present)
3. **Config File** `config/appsettings.json` → `TestSettings.Browser`
4. **Defaults** `chrome` + non-headless mode (fallback)

## Example: Running Tests in All Browsers

### Option 1: Using .env File (Recommended for Local Development)

Create `.env` file in project root:

```bash
# .env - Chrome example
TEST_USER_EMAIL=correctemail@example.com
TEST_USER_PASSWORD=correctpassword
TestSettings__Browser=chrome
```

Run tests:
```powershell
dotnet test tests/UITests/UITests.csproj
```

Switch between browsers by editing the `.env` file and re-running.

### Option 2: Using PowerShell Environment Variables

```powershell
# Chrome tests
dotnet test tests/UITests/UITests.csproj

# Edge tests
$env:TestSettings__Browser = "edge"; dotnet test tests/UITests/UITests.csproj

# Firefox tests
$env:TestSettings__Browser = "firefox"; dotnet test tests/UITests/UITests.csproj
```

### Option 3: Using Config File

Edit `config/appsettings.json`:
```json
"TestSettings": {
  "Browser": "firefox"
}
```

Then run:
```powershell
dotnet test tests/UITests/UITests.csproj
```

### Option 4: Batch/CMD Script

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

## Parallel Execution (Future)

The framework is architected for parallel execution with ThreadLocal WebDriver instances:

- Each test thread gets its own isolated browser instance
- Run same test suite across multiple browsers simultaneously
- Use NUnit's `[Parallelizable]` attribute to enable parallel execution
- Framework automatically manages driver lifecycle per thread

Example configuration (future enhancement):
```json
"BrowserOptions": {
  "SupportedBrowsers": ["chrome", "edge", "firefox"],
  "ParallelExecutionBrowsers": ["chrome", "edge", "firefox"],
  "MaxParallelTests": 3
}
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

## CI/CD Pipeline Integration

### GitHub Actions Example

```yaml
name: Cross-Browser Tests
on: [push, pull_request]

jobs:
  test:
    runs-on: windows-latest
    strategy:
      matrix:
        browser: [chrome, edge, firefox]
    steps:
      - uses: actions/checkout@v3
      - uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '10.0.x'
      - name: Run Tests - ${{ matrix.browser }}
        env:
          TEST_USER_EMAIL: ${{ secrets.TEST_USER_EMAIL }}
          TEST_USER_PASSWORD: ${{ secrets.TEST_USER_PASSWORD }}
          TestSettings__Browser: ${{ matrix.browser }}
        run: dotnet test tests/UITests/UITests.csproj --no-build
```

### Azure Pipelines Example

```yaml
pool:
  vmImage: 'windows-latest'

strategy:
  matrix:
    Chrome:
      browser: chrome
    Edge:
      browser: edge
    Firefox:
      browser: firefox

steps:
  - task: UseDotNet@2
    inputs:
      version: '10.0.x'
  - script: |
      dotnet test tests/UITests/UITests.csproj --no-build
    env:
      TEST_USER_EMAIL: $(TEST_USER_EMAIL)
      TEST_USER_PASSWORD: $(TEST_USER_PASSWORD)
      TestSettings__Browser: $(browser)
    displayName: 'Run Tests on $(browser)'
```

## Practical Scenarios

### Scenario 1: Local Development - Single Browser
Use the .env file approach for quick, iterative testing:
```bash
# .env
TestSettings__Browser=chrome
TestSettings__Headless=false
```

### Scenario 2: Local Testing All Browsers (Headless)
Quick validation across all browsers without UI:
```powershell
@("chrome", "edge", "firefox") | ForEach-Object {
    Write-Host "Testing on $_"
    $env:TestSettings__Browser = $_
    $env:TestSettings__Headless = "true"
    dotnet test tests/UITests/UITests.csproj --no-build
}
```

### Scenario 3: CI/CD Deployment - Parallel Browser Testing
Let the CI/CD platform handle parallel execution:
- GitHub Actions/Azure Pipelines will run same test suite on chrome, edge, firefox simultaneously
- Each job gets isolated environment variables
- Results aggregated from all browsers


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
