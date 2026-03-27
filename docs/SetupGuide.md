# Setup Guide

This guide covers everything you need to do once before running tests: prerequisites, credential configuration, browser settings, and understanding the API test architecture.

---

## Table of Contents

1. [Prerequisites](#1-prerequisites)
2. [Repository Setup](#2-repository-setup)
3. [Test Credentials Setup](#3-test-credentials-setup)
4. [Browser Configuration](#4-browser-configuration)
5. [API Authentication Architecture](#5-api-authentication-architecture)
6. [API Response Validation Architecture](#6-api-response-validation-architecture)
7. [Allure Reporting Prerequisites](#7-allure-reporting-prerequisites)

---

## 1. Prerequisites

### Required Software

| Tool | Minimum Version | Notes |
|------|----------------|-------|
| [.NET SDK](https://dotnet.microsoft.com/download) | 10.0 | Required for build and test execution |
| [Allure CLI](https://docs.qameta.io/allure/#_get_started) | 2.x | Required to generate and open HTML reports |
| Java | 8+ | Required by Allure CLI (if using the standard distribution) |
| Chrome, Edge, or Firefox | Latest stable | At least one browser must be installed |

### Verify Installation

**Windows (PowerShell):**
```powershell
dotnet --version
allure --version
java -version
```

**macOS (Terminal):**
```bash
dotnet --version
allure --version
java -version
```

### Installing Allure CLI

**Windows — via Scoop:**
```powershell
scoop install allure
```

**Windows — via Chocolatey:**
```powershell
choco install allure
```

**macOS — via Homebrew:**
```bash
brew install allure
```

Verify by running `allure --version`. If the command is not found, ensure the Allure `bin` directory is on your `PATH`.

---

## 2. Repository Setup

### Clone and Build

**Windows (PowerShell):**
```powershell
git clone <repository-url>
cd SeleniumAutomationFramework
dotnet restore
dotnet build
```

**macOS (Terminal):**
```bash
git clone <repository-url>
cd SeleniumAutomationFramework
dotnet restore
dotnet build
```

A successful build produces no errors and outputs binaries under `tests/UITests/bin/` and `tests/APITests/bin/`.

---

## 3. Test Credentials Setup

Sensitive test credentials are never stored directly in source files. Instead, they are injected via environment variables using the `${VARIABLE_NAME}` syntax in JSON test data files.

### Required Environment Variables

| Variable | Purpose | Example |
|----------|---------|---------|
| `TEST_USER_EMAIL` | Valid test account email | `user@example.com` |
| `TEST_USER_PASSWORD` | Valid test account password | `your_password_here` |

> **Note:** Credentials shown throughout this guide are placeholder examples only and are not valid.

### How Variable Substitution Works

`resources/testdata/loginData.json` uses placeholders:

```json
{
  "validCredentials": {
    "email": "${TEST_USER_EMAIL}",
    "password": "${TEST_USER_PASSWORD}"
  }
}
```

At test startup, `Framework.Data.JsonDataProvider` automatically:
1. Loads the JSON file
2. Finds `${VARIABLE_NAME}` patterns
3. Replaces them with environment variable values
4. Throws a clear `InvalidOperationException` if a variable is missing

### Option 1: .env File (Recommended for Local Development)

The `.env` file is the simplest approach — no shell setup or IDE restarts required.

**Step 1:** Copy the template from the project root:

**Windows (PowerShell):**
```powershell
Copy-Item .env.example .env
```

**macOS (Terminal):**
```bash
cp .env.example .env
```

**Step 2:** Edit `.env` and fill in your actual values:
```bash
# .env — never commit this file
TEST_USER_EMAIL=user@example.com
TEST_USER_PASSWORD=your_password_here

# Optional browser overrides (uncomment to activate)
# TestSettings__Browser=chrome
# TestSettings__Headless=false
```

**Step 3:** The framework loads `.env` automatically before tests execute. No additional commands are needed.

> `.env` is listed in `.gitignore` and will never be committed to source control.

### Option 2: Session-Based Environment Variables

These are active for the current terminal session only and are discarded when the terminal closes.

**Windows — PowerShell:**
```powershell
$env:TEST_USER_EMAIL = "user@example.com"
$env:TEST_USER_PASSWORD = "your_password_here"
```

**Windows — Command Prompt:**
```cmd
set TEST_USER_EMAIL=user@example.com
set TEST_USER_PASSWORD=your_password_here
```

**macOS — Bash or Zsh:**
```bash
export TEST_USER_EMAIL="user@example.com"
export TEST_USER_PASSWORD="your_password_here"
```

### Option 3: Persistent Environment Variables

Use when you want variables to survive terminal restarts.

**Windows — Via System Properties:**
1. Press `Win + Pause` → click **Environment Variables**
2. Under "User variables", click **New**
3. Add `TEST_USER_EMAIL` and `TEST_USER_PASSWORD`
4. Click **OK** and restart your terminal or IDE

**Windows — Via PowerShell:**
```powershell
[Environment]::SetEnvironmentVariable("TEST_USER_EMAIL", "user@example.com", "User")
[Environment]::SetEnvironmentVariable("TEST_USER_PASSWORD", "your_password_here", "User")
```

**macOS — Edit shell profile:**
```bash
# Open ~/.zshrc (zsh default) or ~/.bash_profile (bash)
echo 'export TEST_USER_EMAIL="user@example.com"' >> ~/.zshrc
echo 'export TEST_USER_PASSWORD="your_password_here"' >> ~/.zshrc
source ~/.zshrc
```

### Option 4: Loading .env Manually in a Shell

If you prefer to source the `.env` file rather than relying on the framework auto-load:

**Windows — PowerShell:**
```powershell
Get-Content .env | ForEach-Object {
    if ($_ -and !$_.StartsWith('#')) {
        $parts = $_ -split '=', 2
        if ($parts.Count -eq 2) {
            [Environment]::SetEnvironmentVariable($parts[0].Trim(), $parts[1].Trim())
        }
    }
}
```

**macOS — Bash/Zsh:**
```bash
set -a
source .env
set +a
```

### Option 5: CI/CD Pipeline

Set secrets in your CI/CD platform and inject them as environment variables during test execution.

**GitHub Actions:**
```yaml
env:
  TEST_USER_EMAIL: ${{ secrets.TEST_USER_EMAIL }}
  TEST_USER_PASSWORD: ${{ secrets.TEST_USER_PASSWORD }}
```

**Azure Pipelines:**
```yaml
variables:
  - group: 'Test Credentials'  # variable group containing TEST_USER_EMAIL and TEST_USER_PASSWORD
```

### Verifying Variables Are Set

**Windows — PowerShell:**
```powershell
$env:TEST_USER_EMAIL
$env:TEST_USER_PASSWORD
# Or list all:
Get-ChildItem env:TEST_USER*
```

**Windows — Command Prompt:**
```cmd
echo %TEST_USER_EMAIL%
echo %TEST_USER_PASSWORD%
```

**macOS:**
```bash
echo "$TEST_USER_EMAIL"
printenv | grep TEST_USER
```

### Troubleshooting Missing Variables

| Symptom | Fix |
|---------|-----|
| `Environment variable 'TEST_USER_EMAIL' not found` error | Set the variable and restart terminal/IDE |
| Variables set but tests still fail | Close and reopen your terminal; confirm with `$env:TEST_USER_EMAIL` |
| Pass locally, fail in CI | Verify secrets are configured in GitHub/Azure platform |
| Variable value has special characters | Quote the value: `$env:VAR = "val!ue"` |

---

## 4. Browser Configuration

The framework supports **Chrome** (default), **Edge**, and **Firefox** with a priority-based resolution system.

### Supported Browsers

| Browser | Config Value | Notes |
|---------|-------------|-------|
| Chrome | `chrome` | Default — used if nothing else is configured |
| Edge | `edge` | Chromium-based |
| Firefox | `firefox` | Gecko engine |

### Configuration Priority (Highest to Lowest)

```
1. Process environment variable  →  TestSettings__Browser=edge
2. .env file variable            →  TestSettings__Browser=edge
3. config/appsettings.json       →  "Browser": "edge"
4. Built-in default              →  chrome (non-headless)
```

### Method 1: .env File (Recommended for Local Development)

Add browser settings to the same `.env` file used for credentials:

```bash
# .env
TEST_USER_EMAIL=user@example.com
TEST_USER_PASSWORD=your_password_here

TestSettings__Browser=edge
TestSettings__Headless=false
```

The framework loads this automatically. Switch browsers by editing the file and re-running tests — no shell commands needed.

### Method 2: Session Environment Variable

**Windows — PowerShell:**
```powershell
$env:TestSettings__Browser = "firefox"
$env:TestSettings__Headless = "true"
```

**macOS — Bash/Zsh:**
```bash
export TestSettings__Browser="firefox"
export TestSettings__Headless="true"
```

### Method 3: appsettings.json (Persistent Default)

Edit `config/appsettings.json` for a permanent team default:

```json
{
  "TestSettings": {
    "Browser": "chrome",
    "Headless": false,
    "ImplicitWaitSeconds": 5,
    "ExplicitWaitSeconds": 15
  }
}
```

### Headless Mode

Running without a visible browser window (`Headless=true`) is ~10–15% faster and is the recommended CI/CD setting.

| Where | Example |
|-------|---------|
| `.env` file | `TestSettings__Headless=true` |
| PowerShell | `$env:TestSettings__Headless = "true"` |
| Bash/Zsh | `export TestSettings__Headless="true"` |
| appsettings.json | `"Headless": true` |

> Use `Headless=false` locally when debugging — you can see the browser actions in real time.

### Troubleshooting Browser Issues

| Issue | Fix |
|-------|-----|
| Tests use wrong browser | Check `$env:TestSettings__Browser` (PowerShell) or `echo $TestSettings__Browser` (Bash) |
| .env browser setting ignored | Verify no higher-priority env var is set in the current shell |
| Headless not activating | Check for case sensitivity: value must be `true` not `True` |
| Driver not found | Install the matching browser or update to the latest WebDriver |

---

## 5. API Authentication Architecture

### Overview

API tests authenticate once and reuse the session token across all test classes in a suite run. This section describes how the auth system is implemented.

### Key Components

| Component | Location | Responsibility |
|-----------|---------|----------------|
| `ApiTestBase` | `tests/APITests/ApiTestBase.cs` | Suite setup, token cache, credential storage |
| `ApiSessionContext` | `src/Framework.API/ApiSessionContext.cs` | Thread-safe (AsyncLocal) token store with a serialized token-renewal lock |
| `AuthClient` | `src/Framework.API/AuthClient.cs` | Login, token extraction, re-authentication |
| `BaseAPIPage` | `src/APIPages/BaseAPIPage.cs` | HTTP request dispatch; renews expired tokens via re-authentication and retries once when needed |

### Authentication Flow

1. Suite setup in `ApiTestBase` initializes shared HTTP and API clients.
2. `EnsureSuiteAuthentication()` is called once from `[OneTimeSetUp]`.
3. If a valid `SuiteSharedToken` already exists (another class ran earlier), it is reused.
4. Otherwise, `PerformInitialAuthentication()` logs in, stores the token in `ApiSessionContext`, and caches it in `SuiteSharedToken`.
5. Credentials are retained in memory (via `StoreCredentials`) for automatic re-auth if the token expires mid-suite.
6. All API requests read the token from `ApiSessionContext` automatically.

### Mid-Suite Token Expiry And Renewal

If the current token is expired or close to expiry before a request is sent:
1. `BaseAPIPage.GetOrRefreshTokenAsync(...)` checks the token state.
2. `AuthClient.ReauthenticateIfStoredCredentialsAsync()` performs a fresh login using the stored credentials.
3. The renewed token is stored in `ApiSessionContext`.
4. The request continues with the renewed token.

If an authenticated request still fails because the token is no longer usable:
1. `BaseAPIPage.SendAsync(...)` enters the existing retry path.
2. `AuthClient.ReauthenticateIfStoredCredentialsAsync()` performs a fresh login.
3. The new token is stored in `ApiSessionContext`.
4. The original request is retried once automatically.

Tests that already completed before expiry remain unaffected.

### Thread Safety

- `ApiSessionContext` uses `AsyncLocal<T>` for execution-context isolation.
- Concurrent token-renewal attempts are serialized by an internal `SemaphoreSlim`.
- All token data is in-memory only and cleared during suite teardown.

### Security Notes

- Credentials are held in memory only for test runtime — not persisted to disk.
- Do not apply this credential-caching strategy in production code.
- Session and credentials are cleared during suite teardown.

### API Test Troubleshooting

| Issue | Check |
|-------|-------|
| Auth fails on setup | Confirm credentials are resolved from env vars / `.env` |
| Token not in session after setup | Confirm `ApiTestBase.OneTimeSetUp` completed without exception |
| Re-auth not triggering | Check `BaseAPIPage` is used for all authenticated calls |
| Parallel run token conflicts | `ApiSessionContext` is `AsyncLocal` — each async context has isolated state |

---

## 6. API Response Validation Architecture

### Overview

The framework provides a fluent response validation API that auto-detects JSON and XML payloads.

### Component Map

| Component | File | Role |
|-----------|------|------|
| `ResponseValidator` | `src/Framework.API/ResponseValidator.cs` | Fluent entry point; owns format detection and validator selection |
| `IResponseValidator` | `src/Framework.API/IResponseValidator.cs` | Interface contract implemented by both format validators |
| `JsonResponseValidator` | `src/Framework.API/JsonResponseValidator.cs` | JSON validation via Newtonsoft.Json JToken; dot-notation and JSONPath |
| `XmlResponseValidator` | `src/Framework.API/XmlResponseValidator.cs` | XML validation via System.Xml.Linq XDocument + XPath |

### Format Detection Rules

1. Prefer the `Content-Type` response header.
2. Fall back to payload shape: `{` or `[` → JSON; `<` → XML.
3. Default to JSON when ambiguous.

### Supported Validation Methods

```csharp
ResponseValidator
    .FromContent(response.ResponseBody)    // or .FromResponse(httpResponseMessage)
    .Validate("data.id", 42)               // exact value
    .ValidateFieldExists("data.email")     // field presence
    .ValidateFieldNotExists("data.secret") // field absence
    .ValidateType("data.id", typeof(int))  // type assertion
    .ValidateContains("message", "success"); // substring match
```

### JSON Validation — Active Test Files

JSON validation is currently used in:
- `tests/APITests/AuthAPITests.cs`
- `tests/APITests/BookingsAPITests.cs`
- `tests/APITests/EventsAPITests.cs`

### XML Validation — Test Coverage

XML validation is covered by sample tests in `tests/APITests/XmlResponseValidatorTests.cs`. Payloads are stored as resource files (not inline strings):

| Resource File | Purpose |
|--------------|---------|
| `resources/testdata/xml/xml-core-assertions-response.xml` | Field exists, value, contains |
| `resources/testdata/xml/xml-complex-paths-response.xml` | Indexed XPath, type assertions, not-exists |
| `resources/testdata/xml/xml-error-shape-response.xml` | Auto-detection, error payload, raw content |

Resource files are copied to `TestData/xml/` in the test output directory. This is configured in `tests/APITests/APITests.csproj`.

Each XML test attaches to the Allure report: XML payload, validation plan, and result summary.

### Best Practices

- Use `ResponseValidator.FromContent(...)` for all API result assertions.
- Prefer `ValidateType(...)` for schema-sensitive fields (IDs, counts).
- Use `ValidateFieldNotExists(...)` to confirm fields are absent in error responses.
- Add XML test coverage only for endpoints that actually produce XML.

---

## 7. Allure Reporting Prerequisites

### Allure Configuration

Allure integration is centralized:

| File | Purpose |
|------|---------|
| `config/allureConfig.json` | Shared Allure configuration for both test projects |
| `src/Framework.Reporting/AllureHooks.cs` | Shared NUnit setup fixture for Allure lifecycle |

Both `UITests.csproj` and `APITests.csproj` link these shared files at build time.

### Output Directories

| Directory | Contents |
|-----------|----------|
| `reports/allure-results/` | Raw test result JSON and attachments (generated per run) |
| `reports/allure-report/` | Compiled HTML report (generated by Allure CLI) |
| `reports/screenshots/` | Screenshots from failed UI tests |

### Test Priority and Allure Severity Mapping

Apply the `[Priority]` attribute at class or method level:

```csharp
[Priority(TestPriority.High)]
[Test]
public void Login_WithValidCredentials() { }
```

| Priority | Allure Severity | NUnit Category |
|----------|----------------|---------------|
| `High` | `critical` | `High`, `Smoke` |
| `Medium` | `normal` | `Medium`, `Sanity` |
| `Low` | `minor` | `Low` |

### Report Contents

The generated Allure report includes:
- Pass/fail/skip/broken totals (overview)
- Suite hierarchy (Suites tab)
- Environment details: browser, OS, framework version, run duration
- Severity grouping based on test priority
- Failure categories: assertions, timeouts, missing elements, HTTP 5xx
- Screenshots and page source attached for failed UI tests
- Request/response payloads attached for failed API tests

For report generation and viewing instructions, see [ExecutionGuide.md](./ExecutionGuide.md#6-allure-reports).

---

## Related Files

- [config/appsettings.json](../config/appsettings.json) — Browser and wait time defaults
- [config/allureConfig.json](../config/allureConfig.json) — Allure configuration
- [src/Framework.Reporting/AllureHooks.cs](../src/Framework.Reporting/AllureHooks.cs) — Allure lifecycle hooks
- [src/Framework.API/ResponseValidator.cs](../src/Framework.API/ResponseValidator.cs)
- [src/Framework.API/ApiSessionContext.cs](../src/Framework.API/ApiSessionContext.cs)
- [src/Framework.API/AuthClient.cs](../src/Framework.API/AuthClient.cs)
- [src/APIPages/BaseAPIPage.cs](../src/APIPages/BaseAPIPage.cs)
- [tests/APITests/ApiTestBase.cs](../tests/APITests/ApiTestBase.cs)
- [src/Framework.Data/JsonDataProvider.cs](../src/Framework.Data/JsonDataProvider.cs)
- [resources/testdata/loginData.json](../resources/testdata/loginData.json)
