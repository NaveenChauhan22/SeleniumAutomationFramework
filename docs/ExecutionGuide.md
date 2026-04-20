# Execution Guide (Windows + macOS)

Use this guide to run tests after setup is complete.

If you have not completed setup yet, follow [SetupGuide.md](./SetupGuide.md) first.

---

## What You Will Achieve

By the end of this guide, you will be able to:
- Run all tests.
- Run only UI or only API tests.
- Run Smoke and Sanity subsets.
- Generate and open Allure reports.

---

## 1. Pre-Run Checklist

### Step 1: Open Terminal in Project Root

Project root is the folder that contains `SeleniumAutomationFramework.sln`.

Why this step: All commands in this guide assume you are in the project root.

### Step 2: Verify Required Commands

**Windows (PowerShell):**
```powershell
dotnet --version
java -version
allure --version
```

**macOS (Terminal):**
```bash
dotnet --version
java -version
allure --version
```

Why this step: It prevents execution failures caused by missing tools.

### Step 3: Confirm Credentials Are Configured

Make sure `.env` exists and contains:

```bash
TEST_USER_EMAIL=your_test_email@example.com
TEST_USER_PASSWORD=your_test_password
```

Why this step: API and UI login tests require valid test account credentials.

### Step 4: Quick Compatibility Checks (Recommended)

Before running tests, validate these points:

1. Shell type matches command block:
  - Windows commands are for **PowerShell**.
  - macOS commands are for **bash/zsh**.

2. Allure major version:
  - Run `allure --version`.
  - Prefer **Allure CLI 2.x** for this framework (recommended 2.38.1).
  - If 3.x is installed, follow rollback instructions in [SetupGuide.md](./SetupGuide.md#step-6-install-allure-cli-after-java).

3. Java is available in PATH:
  - Run `java -version`.

4. Run from repository root:
  - Folder containing `SeleniumAutomationFramework.sln`.

---

## 2. First Standard Run (Recommended)

### Step 1: Restore and Build

**Windows (PowerShell):**
```powershell
dotnet restore
dotnet build .\SeleniumAutomationFramework.sln
```

**macOS (Terminal):**
```bash
dotnet restore
dotnet build ./SeleniumAutomationFramework.sln
```

Why this step: Restore/build confirms code and dependencies are in a healthy state.

### Step 2: Run Full Test Suite

**Windows (PowerShell):**
```powershell
dotnet test .\SeleniumAutomationFramework.sln
```

**macOS (Terminal):**
```bash
dotnet test ./SeleniumAutomationFramework.sln
```

Why this step: This executes both API and UI tests in one command.

### Step 3: Generate Allure Report

**Windows (PowerShell):**
```powershell
allure generate .\reports\allure-results -o .\reports\allure-report --clean
```

**macOS (Terminal):**
```bash
allure generate ./reports/allure-results -o ./reports/allure-report --clean
```

Why this step: It converts raw result files into a readable HTML dashboard.

Important:
- The command must start with `allure`.
- Running only `generate ...` (without `allure`) causes `Unknown Syntax Error: Command not found`.

Optional pre-check if report appears empty:

**Windows (PowerShell):**
```powershell
Get-ChildItem .\reports\allure-results\*.json
```

**macOS (Terminal):**
```bash
ls ./reports/allure-results/*.json
```

### Step 4: Open Allure Report

**Windows (PowerShell):**
```powershell
allure open .\reports\allure-report
```

**macOS (Terminal):**
```bash
allure open ./reports/allure-report
```

Why this step: This lets you review failed tests with screenshots and API payloads.

### Where to Find Your Reports After a Run

| What | Location |
|------|----------|
| Allure HTML report | `reports/allure-report/index.html` — open this in a browser if `allure open` is unavailable |
| Raw test result files | `reports/allure-results/` — JSON files written by each test run |
| Screenshots (failed UI tests) | `reports/screenshots/` — one PNG per failed UI test |
| Test execution log | `reports/logs/` — plain-text log from the latest run |

All these folders are created automatically when tests run. You do not need to create them manually.

---

## 3. Run Specific Test Suites

### Step 1: Run Only API Tests

**Windows (PowerShell):**
```powershell
dotnet test .\tests\APITests\APITests.csproj
```

**macOS (Terminal):**
```bash
dotnet test ./tests/APITests/APITests.csproj
```

Why this step: Use this when validating backend APIs only.

### Step 2: Run Only UI Tests

**Windows (PowerShell):**
```powershell
dotnet test .\tests\UITests\UITests.csproj
```

**macOS (Terminal):**
```bash
dotnet test ./tests/UITests/UITests.csproj
```

Why this step: Use this when validating browser-based UI flows only.

### Step 3: Run Faster Without Rebuild (Optional)

**Windows (PowerShell):**
```powershell
dotnet test .\tests\APITests\APITests.csproj --no-build
dotnet test .\tests\UITests\UITests.csproj --no-build
```

**macOS (Terminal):**
```bash
dotnet test ./tests/APITests/APITests.csproj --no-build
dotnet test ./tests/UITests/UITests.csproj --no-build
```

Why this step: It saves time when code has not changed since last build.

---

## 4. Run By Priority (Smoke and Sanity)

### Step 1: Run Smoke Tests

**Windows (PowerShell):**
```powershell
dotnet test .\SeleniumAutomationFramework.sln --filter "Category=High&Category=Smoke"
```

**macOS (Terminal):**
```bash
dotnet test ./SeleniumAutomationFramework.sln --filter "Category=High&Category=Smoke"
```

Why this step: Smoke tests are a fast confidence check for critical paths.

### Step 2: Run Sanity Tests

**Windows (PowerShell):**
```powershell
dotnet test .\SeleniumAutomationFramework.sln --filter "Category=Sanity"
```

**macOS (Terminal):**
```bash
dotnet test ./SeleniumAutomationFramework.sln --filter "Category=Sanity"
```

Why this step: Sanity tests validate important but broader functionality after smoke.

### Step 3: Run One Test by Name (Optional)

**Windows (PowerShell):**
```powershell
dotnet test .\tests\UITests\UITests.csproj --filter "Name=Login_WithConfiguredRoleCredentials_ShouldAuthenticate"
```

**macOS (Terminal):**
```bash
dotnet test ./tests/UITests/UITests.csproj --filter "Name=Login_WithConfiguredRoleCredentials_ShouldAuthenticate"
```

Why this step: This is useful for quick re-check of a single failed test.

Note: this test is parameterized with multiple role test cases.

### Step 4: Run Role + Smoke Filter (Optional)

**Windows (PowerShell):**
```powershell
$env:TEST_EXECUTION_ROLE = "admin"
dotnet test .\SeleniumAutomationFramework.sln --filter "Category=admin&Category=Smoke"
```

**macOS (Terminal):**
```bash
export TEST_EXECUTION_ROLE=admin
dotnet test ./SeleniumAutomationFramework.sln --filter "Category=admin&Category=Smoke"
```

Why this step: `TestRole` is now an NUnit category, so role and smoke can be filtered together.

---

## 4A. Role-Based Execution

Use this section to run tests for a specific role or mixed-role suite.
This section includes both role execution commands and role-resolution rules in one place.

`[TestRole("...")]` is also an NUnit category, so role and test-type filters can be combined.

Role resolution order:
1. Method-level `[TestRole("...")]`
2. Class-level `[TestRole("...")]`
3. `TEST_EXECUTION_ROLE` environment variable
4. Fail fast if no role is available for auth-dependent tests

Credential lookup order for selected role:
1. `TEST_{ROLE}_EMAIL` and `TEST_{ROLE}_PASSWORD`
2. `roles.{role}` in `resources/testdata/loginData.json`
3. Fail with clear setup error if missing/incomplete

Supported default role keys: `user`, `admin`, `organizer`, `viewer`.

### Run User Role + Smoke Category

**Windows (PowerShell):**
```powershell
$env:TEST_EXECUTION_ROLE = "user"
dotnet test .\SeleniumAutomationFramework.sln --filter "Category=user&Category=Smoke"
```

**macOS (Terminal):**
```bash
export TEST_EXECUTION_ROLE=user
dotnet test ./SeleniumAutomationFramework.sln --filter "Category=user&Category=Smoke"
```

### Run Admin Role + Smoke Category

**Windows (PowerShell):**
```powershell
$env:TEST_EXECUTION_ROLE = "admin"
dotnet test .\SeleniumAutomationFramework.sln --filter "Category=admin&Category=Smoke"
```

**macOS (Terminal):**
```bash
export TEST_EXECUTION_ROLE=admin
dotnet test ./SeleniumAutomationFramework.sln --filter "Category=admin&Category=Smoke"
```

### Run Admin Role + Smoke + API Auth Name Pattern

**Windows (PowerShell):**
```powershell
$env:TEST_EXECUTION_ROLE = "admin"
dotnet test .\tests\APITests\APITests.csproj --filter "Category=admin&Category=Smoke&FullyQualifiedName~Auth"
```

**macOS (Terminal):**
```bash
export TEST_EXECUTION_ROLE=admin
dotnet test ./tests/APITests/APITests.csproj --filter "Category=admin&Category=Smoke&FullyQualifiedName~Auth"
```

### Mixed-Role in a Single Run

If tests are annotated with `[TestRole("user")]` and `[TestRole("admin")]`, one `dotnet test` run will execute them under their declared roles.
`TEST_EXECUTION_ROLE` still acts as credential fallback for tests that do not declare a role.

---

## 5. Browser and Headless Execution

### Step 1: Run UI Tests on Edge

**Windows (PowerShell):**
```powershell
$env:TestSettings__Browser = "edge"
dotnet test .\tests\UITests\UITests.csproj
```

**macOS (Terminal):**
```bash
export TestSettings__Browser="edge"
dotnet test ./tests/UITests/UITests.csproj
```

Why this step: This validates browser compatibility beyond default Chrome.

### Step 2: Run UI Tests on Firefox

**Windows (PowerShell):**
```powershell
$env:TestSettings__Browser = "firefox"
dotnet test .\tests\UITests\UITests.csproj
```

**macOS (Terminal):**
```bash
export TestSettings__Browser="firefox"
dotnet test ./tests/UITests/UITests.csproj
```

Why this step: This checks behavior on a non-Chromium browser engine.

### Step 3: Enable Headless Mode

**Windows (PowerShell):**
```powershell
$env:TestSettings__Headless = "true"
dotnet test .\tests\UITests\UITests.csproj
```

**macOS (Terminal):**
```bash
export TestSettings__Headless="true"
dotnet test ./tests/UITests/UITests.csproj
```

Why this step: Headless mode is faster and ideal for CI-like runs.

### Step 4: Reset Temporary Browser Overrides (Optional)

**Windows (PowerShell):**
```powershell
Remove-Item Env:\TestSettings__Browser -ErrorAction SilentlyContinue
Remove-Item Env:\TestSettings__Headless -ErrorAction SilentlyContinue
```

**macOS (Terminal):**
```bash
unset TestSettings__Browser
unset TestSettings__Headless
```

Why this step: This avoids confusion in future runs caused by old terminal values.

---

## 6. Sequential Run for Debugging

### Step 1: Force Single-Threaded Execution

**Windows (PowerShell):**
```powershell
dotnet test .\SeleniumAutomationFramework.sln -- NUnit.NumberOfTestWorkers=0
```

**macOS (Terminal):**
```bash
dotnet test ./SeleniumAutomationFramework.sln -- NUnit.NumberOfTestWorkers=0
```

Why this step: Sequential mode is easier to debug when failures are hard to reproduce.

---

## 7. Basic Troubleshooting

| Problem | What to Check |
|---------|---------------|
| `dotnet test` fails immediately | Run `dotnet restore` and `dotnet build` again |
| `dotnet restore` fails behind proxy/firewall | Configure NuGet/proxy access and retry |
| `allure` command not found | Install Allure CLI and verify with `allure --version` |
| Allure works but output is inconsistent | Check `allure --version`; prefer CLI 2.x for this framework |
| `Unknown Syntax Error: Command not found` for Allure | Use full command: `allure generate .\reports\allure-results -o .\reports\allure-report --clean` |
| Tests fail with auth/credential error | Check `.env` values for `TEST_USER_EMAIL` and `TEST_USER_PASSWORD` |
| `.env` exists but credentials still missing | Ensure file is exactly `.env` (not `.env.txt`) in repository root |
| Wrong browser opens | Check `TestSettings__Browser` in `.env` and active terminal variables |
| Driver startup fails on first run | Ensure browser is installed and first-time driver download is allowed |
| Allure report is empty | Verify files exist in `reports/allure-results` before running `allure generate` |
| HTML opened but not Allure-looking report | Open `reports/allure-report/index.html` after generating from `reports/allure-results` |

---

## 8. Useful Configuration Settings

All default behaviour is controlled by [config/appsettings.json](../config/appsettings.json). Open the file in VS Code and change any value — no terminal commands needed. The change takes effect on the next test run.

### Full Settings Reference

#### TestSettings

| Key | Default | What it controls |
|-----|---------|------------------|
| `Browser` | `chrome` | Which browser runs UI tests. Allowed values: `chrome`, `edge`, `firefox` |
| `Headless` | `false` | `true` = browser runs invisibly (faster, good for CI). `false` = browser window is visible (good for debugging) |
| `ImplicitWaitSeconds` | `5` | How long the driver waits automatically before reporting an element as not found |
| `ExplicitWaitSeconds` | `15` | Maximum time an explicit wait (e.g. wait until button appears) will keep retrying before failing |
| `ExplicitWaitPollingMs` | `250` | How often (in milliseconds) an explicit wait checks the condition again |
| `PageLoadTimeoutSeconds` | `60` | Maximum time allowed for a full page to load before the test fails |
| `ScriptTimeoutSeconds` | `30` | Maximum time allowed for a JavaScript command to complete |
| `Environment` | `local` | Labels the test run environment. Appears in the Allure report for traceability |
| `AllureResultsFolder` | `allure-results` | Subfolder name where raw Allure result files are written |

#### Reporting

| Key | Default | What it controls |
|-----|---------|------------------|
| `EnableExecutionHistory` | `false` | `true` = Allure report shows a trend graph of previous runs. Requires keeping the `reports/allure-report/history` folder between runs |

#### Api

| Key | Default | What it controls |
|-----|---------|------------------|
| `BaseUrl` | *(set in file)* | The root URL for all API test requests |
| `ShowBearerToken` | `false` | Controls whether the auth token is visible in Allure report attachments — see below |

> **Priority rule:** A value set in `.env` or as an environment variable always wins over `appsettings.json`. Use `appsettings.json` for team-wide or machine-wide defaults, and `.env` for personal overrides.

---

### ShowBearerToken

```json
"Api": {
  "ShowBearerToken": false
}
```

| Value | What happens |
|-------|--------------|
| `false` (default) | The Bearer token is masked in all Allure report attachments and logs — safe for sharing reports |
| `true` | The full token value is printed in Allure request/response attachments — useful when debugging API authentication failures |

Why this setting exists: API tests log request and response details to the Allure report. A Bearer token is a sensitive credential. By default it is hidden so reports can be shared without leaking auth tokens.

When to set it to `true`: Only temporarily, on your local machine, when an API test is failing with an auth error and you need to inspect the exact token being sent. Set it back to `false` before committing or sharing the report.

---

## 9. Advanced (Optional)

- CI pipeline file: [ci/azure-pipelines.yml](../ci/azure-pipelines.yml)
- Framework can run API and UI tests in parallel through NUnit.
- For day-to-day manual usage, sections 1 to 8 are enough.

Why this section: Keep advanced engineering details separate from everyday execution flow.
