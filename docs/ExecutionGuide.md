# Execution Guide

This guide covers how to run tests, target specific browsers, filter by category, generate Allure reports, and integrate with CI/CD pipelines.

---

## Table of Contents

1. [Quick Start](#1-quick-start)
2. [Running Tests by Suite](#2-running-tests-by-suite)
3. [Cross-Browser Execution](#3-cross-browser-execution)
4. [Headless Mode](#4-headless-mode)
5. [NUnit Test Filtering](#5-nunit-test-filtering)
6. [Allure Reports](#6-allure-reports)
7. [CI/CD Pipeline Integration](#7-cicd-pipeline-integration)
8. [Troubleshooting](#8-troubleshooting)

---

## 1. Quick Start

Ensure you have completed [SetupGuide.md](./SetupGuide.md) first (credentials, browser config, prerequisites).

### Build and Run All Tests

**Windows (PowerShell):**
```powershell
dotnet restore
dotnet build
dotnet test .\SeleniumAutomationFramework.sln
```

**macOS (Terminal):**
```bash
dotnet restore
dotnet build
dotnet test ./SeleniumAutomationFramework.sln
```

---

## 2. Running Tests by Suite

### API Tests Only

**Windows (PowerShell):**
```powershell
dotnet test .\tests\APITests\APITests.csproj
```

**macOS (Terminal):**
```bash
dotnet test ./tests/APITests/APITests.csproj
```

### UI Tests Only

**Windows (PowerShell):**
```powershell
dotnet test .\tests\UITests\UITests.csproj
```

**macOS (Terminal):**
```bash
dotnet test ./tests/UITests/UITests.csproj
```

### Skip Rebuild (Faster after Initial Build)

Add `--no-build` to skip compilation when source has not changed:

**Windows (PowerShell):**
```powershell
dotnet test .\tests\UITests\UITests.csproj --no-build
dotnet test .\tests\APITests\APITests.csproj --no-build
```

**macOS (Terminal):**
```bash
dotnet test ./tests/UITests/UITests.csproj --no-build
dotnet test ./tests/APITests/APITests.csproj --no-build
```

---

## 3. Cross-Browser Execution

The default browser is **Chrome**. You can override it using environment variables, the `.env` file, or `appsettings.json`.

### Via Environment Variable (Inline — One-Liner)

**Windows (PowerShell):**
```powershell
# Chrome (default)
dotnet test .\tests\UITests\UITests.csproj

# Edge
$env:TestSettings__Browser = "edge"; dotnet test .\tests\UITests\UITests.csproj

# Firefox
$env:TestSettings__Browser = "firefox"; dotnet test .\tests\UITests\UITests.csproj
```

**macOS (Bash/Zsh):**
```bash
# Chrome (default)
dotnet test ./tests/UITests/UITests.csproj

# Edge
export TestSettings__Browser="edge"; dotnet test ./tests/UITests/UITests.csproj

# Firefox
export TestSettings__Browser="firefox"; dotnet test ./tests/UITests/UITests.csproj
```

**Windows (Command Prompt):**
```cmd
set TestSettings__Browser=edge
dotnet test tests\UITests\UITests.csproj
```

### Via .env File (Recommended for Local Development)

Edit the `.env` file in the project root and re-run tests:

```bash
# .env
TestSettings__Browser=firefox
TestSettings__Headless=false
```

```powershell
# Windows
dotnet test .\tests\UITests\UITests.csproj
```

```bash
# macOS
dotnet test ./tests/UITests/UITests.csproj
```

### Via appsettings.json (Persistent Default)

Edit `config/appsettings.json`:

```json
{
  "TestSettings": {
    "Browser": "edge"
  }
}
```

Then run tests normally:

**Windows (PowerShell):**
```powershell
dotnet test .\tests\UITests\UITests.csproj
```

**macOS (Terminal):**
```bash
dotnet test ./tests/UITests/UITests.csproj
```

### Configuration Priority

```
1. Process environment variable  (highest — overrides everything)
2. .env file variable
3. config/appsettings.json
4. Built-in default: chrome, non-headless
```

### Running All Browsers in Sequence

**Windows (PowerShell):**
```powershell
# Chrome
dotnet test .\tests\UITests\UITests.csproj

# Edge
$env:TestSettings__Browser = "edge"
dotnet test .\tests\UITests\UITests.csproj

# Firefox
$env:TestSettings__Browser = "firefox"
dotnet test .\tests\UITests\UITests.csproj

# Clear override
Remove-Item Env:\TestSettings__Browser
```

**macOS (Bash/Zsh):**
```bash
# Chrome
dotnet test ./tests/UITests/UITests.csproj

# Edge
export TestSettings__Browser="edge"
dotnet test ./tests/UITests/UITests.csproj

# Firefox
export TestSettings__Browser="firefox"
dotnet test ./tests/UITests/UITests.csproj

# Clear override
unset TestSettings__Browser
```

---

## 4. Headless Mode

Headless mode runs the browser without a visible window. It is ~10–15% faster and recommended for CI/CD.

### Enable Headless

**Windows (PowerShell):**
```powershell
$env:TestSettings__Headless = "true"
dotnet test .\tests\UITests\UITests.csproj
```

**macOS (Bash/Zsh):**
```bash
export TestSettings__Headless="true"
dotnet test ./tests/UITests/UITests.csproj
```

**Via .env file:**
```bash
TestSettings__Headless=true
```

**Via appsettings.json:**
```json
{
  "TestSettings": {
    "Headless": true
  }
}
```

### Disable Headless (Visual Debug Mode)

**Windows (PowerShell):**
```powershell
$env:TestSettings__Headless = "false"
dotnet test .\tests\UITests\UITests.csproj
```

**macOS (Bash/Zsh):**
```bash
export TestSettings__Headless="false"
dotnet test ./tests/UITests/UITests.csproj
```

> Use `Headless=false` when debugging — you can observe every browser step in real time.

---

## 5. NUnit Test Filtering

Tests are assigned NUnit categories via the `[Priority]` attribute.

### Priority-to-Category Mapping

| Priority | Categories Applied |
|----------|--------------------|
| `High` | `Category("High")`, `Category("Smoke")` |
| `Medium` | `Category("Medium")`, `Category("Sanity")` |
| `Low` | `Category("Low")` |

### Run a Single Test by Name

**Windows (PowerShell):**
```powershell
dotnet test .\tests\UITests\UITests.csproj --filter "Name=Login_WithValidCredentials"
dotnet test .\tests\APITests\APITests.csproj --filter "Name=MeEndpoint_WithValidToken_ShouldReturnCurrentUser"
```

**macOS (Bash/Zsh):**
```bash
dotnet test ./tests/UITests/UITests.csproj --filter "Name=Login_WithValidCredentials"
dotnet test ./tests/APITests/APITests.csproj --filter "Name=MeEndpoint_WithValidToken_ShouldReturnCurrentUser"
```

Use `FullyQualifiedName~` for a partial name match:

**Windows (PowerShell):**
```powershell
dotnet test .\tests\UITests\UITests.csproj --filter "FullyQualifiedName~Login_WithValidCredentials"
```

**macOS (Bash/Zsh):**
```bash
dotnet test ./tests/UITests/UITests.csproj --filter "FullyQualifiedName~Login_WithValidCredentials"
```

### Run by Category — Suite Scope

**Windows (PowerShell):**
```powershell
# High priority only
dotnet test .\tests\UITests\UITests.csproj --filter "Category=High"
dotnet test .\tests\APITests\APITests.csproj --filter "Category=High"

# Sanity (Medium priority)
dotnet test .\tests\UITests\UITests.csproj --filter "Category=Sanity"
dotnet test .\tests\APITests\APITests.csproj --filter "Category=Sanity"

# Must have both High and Smoke
dotnet test .\tests\UITests\UITests.csproj --filter "Category=High&Category=Smoke"
dotnet test .\tests\APITests\APITests.csproj --filter "Category=High&Category=Smoke"
```

**macOS (Bash/Zsh):**
```bash
# High priority only
dotnet test ./tests/UITests/UITests.csproj --filter "Category=High"
dotnet test ./tests/APITests/APITests.csproj --filter "Category=High"

# Sanity (Medium priority)
dotnet test ./tests/UITests/UITests.csproj --filter "Category=Sanity"
dotnet test ./tests/APITests/APITests.csproj --filter "Category=Sanity"

# Must have both High and Smoke
dotnet test ./tests/UITests/UITests.csproj --filter "Category=High&Category=Smoke"
dotnet test ./tests/APITests/APITests.csproj --filter "Category=High&Category=Smoke"
```

### Run by Category — Solution Scope (Both Projects)

**Windows (PowerShell):**
```powershell
dotnet test .\SeleniumAutomationFramework.sln --filter "Category=High"
dotnet test .\SeleniumAutomationFramework.sln --filter "Category=High&Category=Smoke"
dotnet test .\SeleniumAutomationFramework.sln --filter "Category=Medium&Category=Sanity"
```

**macOS (Bash/Zsh):**
```bash
dotnet test ./SeleniumAutomationFramework.sln --filter "Category=High"
dotnet test ./SeleniumAutomationFramework.sln --filter "Category=High&Category=Smoke"
dotnet test ./SeleniumAutomationFramework.sln --filter "Category=Medium&Category=Sanity"
```

### Composite Filters

**Exclude a category:**

```powershell
# Windows
dotnet test .\SeleniumAutomationFramework.sln --filter "Category!=Sanity"
```

```bash
# macOS
dotnet test ./SeleniumAutomationFramework.sln --filter "Category!=Sanity"
```

**Combine name and category:**

```powershell
# Windows — UI Login tests that are High priority
dotnet test .\tests\UITests\UITests.csproj --filter "FullyQualifiedName~Login&Category=High"
```

```bash
# macOS — UI Login tests that are High priority
dotnet test ./tests/UITests/UITests.csproj --filter "FullyQualifiedName~Login&Category=High"
```

### List All Discovered Tests

**Windows (PowerShell):**
```powershell
dotnet test .\tests\UITests\UITests.csproj --list-tests
dotnet test .\tests\APITests\APITests.csproj --list-tests
```

**macOS (Bash/Zsh):**
```bash
dotnet test ./tests/UITests/UITests.csproj --list-tests
dotnet test ./tests/APITests/APITests.csproj --list-tests
```

---

## 6. Allure Reports

### Generate the Report

Run tests first — results are written to `reports/allure-results/` automatically. Then generate the HTML report:

**Windows (PowerShell):**
```powershell
allure generate .\reports\allure-results -o .\reports\allure-report --clean
```

**macOS (Terminal):**
```bash
allure generate ./reports/allure-results -o ./reports/allure-report --clean
```

> The framework attempts to auto-generate the report after each run when Allure CLI is on `PATH`.

### Open the Report

**Windows (PowerShell):**
```powershell
allure open .\reports\allure-report
```

**macOS (Terminal):**
```bash
allure open ./reports/allure-report
```

This starts a local web server and opens the report in your default browser.

### Full Run + Report in One Command

**Windows (PowerShell):**
```powershell
dotnet test .\SeleniumAutomationFramework.sln
allure generate .\reports\allure-results -o .\reports\allure-report --clean
allure open .\reports\allure-report
```

**macOS (Terminal):**
```bash
dotnet test ./SeleniumAutomationFramework.sln
allure generate ./reports/allure-results -o ./reports/allure-report --clean
allure open ./reports/allure-report
```

### Failure Attachments

| Test Type | What Is Attached on Failure |
|-----------|----------------------------|
| UI Tests | Screenshot (also saved to `reports/screenshots/`), page source |
| API Tests | Request payload, response payload |
| XML Tests | XML payload, validation plan, validation result |

---

## 7. CI/CD Pipeline Integration

### GitHub Actions — Cross-Browser Matrix

```yaml
name: Cross-Browser Tests

on: [push, pull_request]

jobs:
  test:
    runs-on: ${{ matrix.os }}
    strategy:
      matrix:
        os: [windows-latest, macos-latest]
        browser: [chrome, edge, firefox]

    steps:
      - uses: actions/checkout@v3

      - uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '10.0.x'

      - name: Restore and build
        run: |
          dotnet restore
          dotnet build --no-restore

      - name: Run Tests — ${{ matrix.browser }} on ${{ matrix.os }}
        env:
          TEST_USER_EMAIL: ${{ secrets.TEST_USER_EMAIL }}
          TEST_USER_PASSWORD: ${{ secrets.TEST_USER_PASSWORD }}
          TestSettings__Browser: ${{ matrix.browser }}
          TestSettings__Headless: "true"
        run: dotnet test SeleniumAutomationFramework.sln --no-build

      - name: Generate Allure Report
        if: always()
        run: allure generate reports/allure-results -o reports/allure-report --clean

      - name: Upload Allure Report
        if: always()
        uses: actions/upload-artifact@v3
        with:
          name: allure-report-${{ matrix.browser }}-${{ matrix.os }}
          path: reports/allure-report/
```

### GitHub Actions — Smoke Tests Only

```yaml
- name: Run Smoke Tests
  env:
    TEST_USER_EMAIL: ${{ secrets.TEST_USER_EMAIL }}
    TEST_USER_PASSWORD: ${{ secrets.TEST_USER_PASSWORD }}
    TestSettings__Headless: "true"
  run: dotnet test SeleniumAutomationFramework.sln --no-build --filter "Category=High&Category=Smoke"
```

### Azure Pipelines — Cross-Browser Matrix

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
      dotnet restore
      dotnet build --no-restore
    displayName: 'Build'

  - script: dotnet test SeleniumAutomationFramework.sln --no-build
    env:
      TEST_USER_EMAIL: $(TEST_USER_EMAIL)
      TEST_USER_PASSWORD: $(TEST_USER_PASSWORD)
      TestSettings__Browser: $(browser)
      TestSettings__Headless: "true"
    displayName: 'Run Tests — $(browser)'

  - script: |
      allure generate reports/allure-results -o reports/allure-report --clean
    displayName: 'Generate Allure Report'
    condition: always()
```

**macOS agent** — replace `vmImage: 'windows-latest'` with `vmImage: 'macOS-latest'`. All `dotnet test` and `allure` commands are identical.

### Recommended CI Settings

| Setting | Recommendation |
|---------|----------------|
| Headless | Always `true` in CI |
| Credentials | Use platform secrets — never hard-code |
| Build step | Always run `dotnet build --no-restore` separately before test |
| Report | Generate Allure report with `--clean` and upload as artifact |
| Filter | Use `--filter "Category=High&Category=Smoke"` for PR checks; full suite for nightly |

---

## 8. Troubleshooting

### Tests Use the Wrong Browser

1. Check active env var: `$env:TestSettings__Browser` (PowerShell) or `echo $TestSettings__Browser` (Bash)
2. Check `.env` file for `TestSettings__Browser=...`
3. Check `config/appsettings.json`
4. Default is always `chrome`

### .env File Not Loading

1. Verify the file is named exactly `.env` (no extension) in the project root
2. Confirm format is `KEY=VALUE` with no spaces around `=`
3. Comments must start with `#`
4. Restart your terminal if it was open before creating the file

### Allure Command Not Found

1. Confirm Allure CLI is installed: `allure --version`
2. Check that the Allure `bin` directory is on `PATH`
3. On macOS: `brew install allure` is the simplest fix
4. On Windows: `scoop install allure` or `choco install allure`

### Tests Pass Locally but Fail in CI

1. Verify `TEST_USER_EMAIL` and `TEST_USER_PASSWORD` secrets are configured in the CI platform
2. Confirm `TestSettings__Headless=true` is set (some browsers fail without headless in CI)
3. Check that the correct browser driver version is available on the CI image

### Build Succeeds but Tests Do Not Run

1. Ensure test projects reference NUnit and NUnit3TestAdapter packages
2. Run `dotnet test --list-tests` to verify tests are discovered
3. Check that filter expressions use correct syntax (no typos in category names)

### Allure Report Is Empty After Test Run

1. Confirm `reports/allure-results/` contains `.json` files after the run
2. Check that `config/allureConfig.json` is correctly linked in both `.csproj` files
3. Re-run `allure generate` with `--clean` to force a fresh report

---

## Related Files

- [SetupGuide.md](./SetupGuide.md) — Prerequisites, credentials, browser config
- [AutomationCodingStandards.md](./AutomationCodingStandards.md) — Code style and test naming conventions
- [GitWorkflow.md](./GitWorkflow.md) — Branching and commit conventions
- [FrameworkArchitecture.md](./FrameworkArchitecture.md) — High-level system design
- [config/appsettings.json](../config/appsettings.json) — Browser and wait time defaults
- [config/allureConfig.json](../config/allureConfig.json) — Allure configuration
