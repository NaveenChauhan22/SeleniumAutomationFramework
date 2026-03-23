# Browser Configuration Guide

This document provides a comprehensive overview of the flexible browser and headless configuration system.

## Overview

The framework supports **Chrome** (default), **Edge**, and **Firefox** browsers with multiple configuration methods prioritizing:
1. Environment variables (highest priority - good for CI/CD)
2. .env file (recommended for local development)
3. appsettings.json (fallback configuration)
4. Hardcoded defaults (fallback: chrome, non-headless)

## Supported Browsers

| Browser  | Config Value | Environment Variable | Notes |
|----------|--------------|----------------------|-------|
| Chrome   | `chrome`     | `TestSettings__Browser=chrome` | Default, most tested |
| Edge     | `edge`       | `TestSettings__Browser=edge` | Chromium-based |
| Firefox  | `firefox`    | `TestSettings__Browser=firefox` | Gecko engine |

## Configuration Methods

### Method 1: .env File (Recommended for Local Development)

**Easiest and most flexible for local testing:**

```bash
# .env (project root)
TEST_USER_EMAIL=correctemail@example.com
TEST_USER_PASSWORD=correctpassword

# Browser Configuration - Uncomment to override
TestSettings__Browser=chrome
TestSettings__Headless=false
```

**How it works:**
- Framework automatically loads .env file before tests execute
- Credentials and browser settings are set as environment variables
- No manual shell setup required (PowerShell/Bash/Zsh)
- Perfect for iterative local development

**Usage:**
```powershell
# Just run tests directly - .env is loaded automatically
dotnet test tests/UITests/UITests.csproj

# Modify .env file to switch browsers, then re-run
```

### Method 2: Environment Variables (Best for CI/CD)

**Set environment variables directly for CI/CD pipelines:**

```powershell
# PowerShell
$env:TestSettings__Browser = "edge"
$env:TestSettings__Headless = "true"
dotnet test tests/UITests/UITests.csproj
```

```batch
# Command Prompt
set TestSettings__Browser=firefox
set TestSettings__Headless=false
dotnet test tests/UITests/UITests.csproj
```

```bash
# Bash/Zsh (macOS/Linux)
export TestSettings__Browser=firefox
export TestSettings__Headless=false
dotnet test tests/UITests/UITests.csproj
```

**When to use:**
- CI/CD pipelines (GitHub Actions, Azure Pipelines, Jenkins)
- Cross-platform compatibility needed
- Multiple test runs with different browsers in sequence

### Method 3: Configuration File

**Edit appsettings.json for persistent configuration:**

```json
{
  "TestSettings": {
    "Browser": "edge",
    "Headless": true,
    "ImplicitWaitSeconds": 5,
    "ExplicitWaitSeconds": 15
  }
}
```

**When to use:**
- Permanent default browser setting
- All developers should use the same browser
- Configuration changes less frequently

### Method 4: System Environment Variables

**Set permanent system-level environment variables:**

Windows:
```powershell
[Environment]::SetEnvironmentVariable("TestSettings__Browser", "firefox", "User")
[Environment]::SetEnvironmentVariable("TestSettings__Headless", "true", "User")
```

macOS:
```bash
# Add to ~/.zshrc (or ~/.bash_profile), then reload shell
echo 'export TestSettings__Browser="firefox"' >> ~/.zshrc
echo 'export TestSettings__Headless="true"' >> ~/.zshrc
source ~/.zshrc
```

**When to use:**
- Never - this pollutes system environment
- Use Method 1 (.env) or Method 2 (process-level env vars) instead

## Headless Mode Configuration

Run browser in headless mode (no UI, faster execution):

### Via .env File
```bash
TestSettings__Headless=true
```

### Via Environment Variable
```powershell
$env:TestSettings__Headless = "true"; dotnet test tests/UITests/UITests.csproj
```

```bash
export TestSettings__Headless=true; dotnet test tests/UITests/UITests.csproj
```

### Via appsettings.json
```json
"TestSettings": {
  "Headless": true
}
```

**Performance Impact:**
- Headless mode: ~10-15% faster
- Useful for CI/CD pipelines
- Not recommended for local debugging (can't see browser)

## Configuration Priority (Resolution Order)

The framework resolves browser configuration in this exact order:

```
1. Process Environment Variable (TestSettings__Browser)
  └─ Set via: $env:VAR = "value", set VAR=value, or export VAR=value
   └─ Highest priority - overrides everything

2. .env File Variables  
   └─ Set via: TestSettings__Browser=edge in .env
   └─ Auto-loaded at test startup
   └─ Perfect for local development

3. appsettings.json Configuration
   └─ Set via: {"TestSettings": {"Browser": "firefox"}}
   └─ Falls back if no env var or .env

4. Default Value (chrome, non-headless)
   └─ Fallback if nothing else configured
   └─ Guaranteed to always work
```

**Example Resolution:**

Scenario A - Using .env with no env var override:
```
1. Check: environment variable TestSettings__Browser → (empty)
2. Check: .env file → TestSettings__Browser=edge (USED)
3. Result: Edge browser
```

Scenario B - Environment variable overrides everything:
```
1. Check: environment variable TestSettings__Browser → chrome (USED)
2. (.env and config file are ignored)
3. Result: Chrome browser
```

Scenario C - No configuration at all:
```
1. Check: Environment variable → (empty)
2. Check: .env file → (doesn't exist)
3. Check: appsettings.json → {"Browser": "chrome"}
4. Result: Chrome browser
```

## Real-World Usage Examples

### Example 1: Local Development - Switch Between Browsers

Edit `.env`:
```bash
# Testing Login page in Edge
TestSettings__Browser=edge
TestSettings__Headless=false
```

Run tests:
```powershell
dotnet test tests/UITests/UITests.csproj
```

macOS/Linux:
```bash
dotnet test tests/UITests/UITests.csproj
```

Switch to Firefox:
```bash
# Change .env
TestSettings__Browser=firefox
```

Re-run tests - automatically uses Firefox.

### Example 2: Quick Headless Testing

```bash
# .env
TestSettings__Browser=chrome
TestSettings__Headless=true
```

Fast test execution without UI (useful during intensive debugging).

### Example 3: CI/CD Pipeline - Matrix Testing

**GitHub Actions:**
```yaml
strategy:
  matrix:
    browser: [chrome, edge, firefox]

steps:
  - name: Run Tests
    env:
      TestSettings__Browser: ${{ matrix.browser }}
```

Runs tests 3 times in parallel, each in different browser.

### Example 4: Pre-commit Hook - Quick Validation

```powershell
# Quick test on default chrome
$env:TestSettings__Headless = "true"
dotnet test tests/UITests/UITests.csproj --no-build

# Full visual test on Edge
$env:TestSettings__Browser = "edge"
$env:TestSettings__Headless = "false"
dotnet test tests/UITests/UITests.csproj --no-build
```

## Troubleshooting

### Tests use wrong browser
1. Check process environment: `$env:TestSettings__Browser` (PowerShell) or `echo "$TestSettings__Browser"` (Bash/Zsh)
2. Check .env file exists and has `TestSettings__Browser=xxx`
3. Check appsettings.json has correct value
4. Default is always chrome

### .env file not loading
1. Verify .env file exists in project root
2. Check file has correct format: `KEY=VALUE`
3. No spaces around `=` sign in .env
4. Comments must start with `#`

### Headless not working
1. Verify `TestSettings__Headless=true` (case-sensitive)
2. Check `Headless` key in appsettings.json if using config file
3. Headless is browser-specific - may not work for all scenarios

## Future: Parallel Browser Execution

The framework is architected for parallel execution across browsers:

```csharp
// Future configuration (not yet implemented)
"BrowserOptions": {
  "SupportedBrowsers": ["chrome", "edge", "firefox"],
  "ParallelExecutionBrowsers": ["chrome", "edge"],  // Run on these simultaneously
  "MaxParallelTests": 2
}
```

Thread-local WebDriver instances ensure each test thread gets isolated browser:
- Safe for concurrent execution
- No browser instance sharing between threads
- Ready for NUnit `[Parallelizable]` attribute

## Best Practices

1. **Local Development**: Use .env file with `Headless=false` for visual feedback
2. **CI/CD Pipelines**: Use environment variables for matrix testing by browser
3. **Team Projects**: Keep appsettings.json with sensible defaults, override in CI only
4. **Performance Testing**: Use `Headless=true` for faster execution
5. **Debugging**: Use `Headless=false` to watch test execution
6. **Never commit**: Keep .env with real credentials in .gitignore

## See Also

- [CrossBrowserExecution.md](./CrossBrowserExecution.md) - Complete cross-browser testing guide
- [FrameworkArchitecture.md](./FrameworkArchitecture.md) - Overall framework design
- [SecureCredentialsSetup.md](./SecureCredentialsSetup.md) - Credential configuration

## Note
Credentials mentioned above are just for reference purpose and are not correct. 