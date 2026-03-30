# Execution Guide

This guide covers local execution modes, NUnit-based parallel execution, cross-browser strategy, CI/CD setup and execution, Allure reporting, and what is implemented in this framework.

---

## Table of Contents

1. [Quick Start](#1-quick-start)
2. [Execution Modes](#2-execution-modes)
3. [Running Tests by Suite](#3-running-tests-by-suite)
4. [Cross-Browser Execution](#4-cross-browser-execution)
5. [Headless Mode](#5-headless-mode)
6. [NUnit Test Filtering](#6-nunit-test-filtering)
7. [Allure Reports](#7-allure-reports)
8. [CI/CD Pipeline Integration](#8-cicd-pipeline-integration)
9. [What Is Implemented](#9-what-is-implemented)
10. [Troubleshooting](#10-troubleshooting)

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

## 2. Execution Modes

### 2.1 Parallel Run (Default)

UI fixtures are annotated with `[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]` + `[Parallelizable(ParallelScope.All)]`, API fixtures use `[Parallelizable(ParallelScope.Self)]`, and the test assemblies receive `[assembly: LevelOfParallelism(4)]` from `tests/AssemblyInfo.cs` (included by `tests/Directory.Build.props`). NUnit distributes eligible tests across up to 4 worker threads automatically — no runsettings file required.

**Windows (PowerShell):**
```powershell
dotnet test .\SeleniumAutomationFramework.sln
```

**macOS (Terminal):**
```bash
dotnet test ./SeleniumAutomationFramework.sln
```

What runs in parallel:
- UI test methods inside `LoginTests` and `HomeNavigationTests` (same browser process)
- `AuthAPITests` ↔ `BookingsAPITests` ↔ `EventsAPITests` (API)

### 2.2 Sequential Run (Debug / Isolation)

Pass `NUnit.NumberOfTestWorkers=0` directly on the command line to override the assembly-level setting and force single-threaded execution. No runsettings file needed.

**Windows (PowerShell):**
```powershell
dotnet test .\SeleniumAutomationFramework.sln -- NUnit.NumberOfTestWorkers=0
```

**macOS (Terminal):**
```bash
dotnet test ./SeleniumAutomationFramework.sln -- NUnit.NumberOfTestWorkers=0
```

> The `--` separator passes options directly to the NUnit adapter at runtime, overriding in-code settings without changing source.

### 2.3 Cross-Browser Runs

Run the same UI tests against different browsers by setting the `TestSettings__Browser` environment variable. Each invocation is a separate process with its own browser instance — no fixture-level conflict.

**Windows (PowerShell):**
```powershell
# Chrome (default)
dotnet test .\tests\UITests\UITests.csproj

# Edge
$env:TestSettings__Browser = "edge"
dotnet test .\tests\UITests\UITests.csproj

# Firefox
$env:TestSettings__Browser = "firefox"
dotnet test .\tests\UITests\UITests.csproj
```

**macOS (Bash/Zsh):**
```bash
export TestSettings__Browser="edge"
dotnet test ./tests/UITests/UITests.csproj
```

To run multiple browsers in true parallel, open **separate terminals** (or use a CI matrix — see Section 8) and set the env var per terminal before running.

### 2.4 API + UI Together

Both suites write to the same `reports/allure-results/` folder. Run both projects in one command:

**Windows (PowerShell):**
```powershell
dotnet test .\SeleniumAutomationFramework.sln
```

**macOS (Terminal):**
```bash
dotnet test ./SeleniumAutomationFramework.sln
```

NUnit's parallel workers will run API fixtures and UI fixtures concurrently within that single invocation.

---

## 3. Running Tests by Suite

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

## 4. Cross-Browser Execution

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

### CI Tiered Cross-Browser Strategy

Running every test on every browser triples CI execution time without proportional coverage gain. The pipeline applies a tiered strategy where browser scope is matched to test priority:

| Browser | CI Filter | What Runs |
|---------|-----------|------------|
| **Chrome** | none (full suite) | All tests — primary regression browser |
| **Firefox** | `Category=High` | High priority tests — validates key flows cross-browser |
| **Edge** | `Category=High&Category=Smoke` | Smoke tests only — fast sanity check |

This gives full regression coverage on Chrome, meaningful cross-browser validation on Firefox, and a fast sanity signal on Edge — without tripling CI minutes.

> **Local runs are unaffected.** When running locally, all browsers execute the full suite by default. The tiered filter is CI-only, applied via `BROWSER_FILTER` per matrix entry in the workflow.
>
> **Smoke runs override the tier.** When `suite=smoke` is selected (PR or manual dispatch), every browser falls back to `Category=High&Category=Smoke` regardless of its tier.

---

## 5. Headless Mode

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

## 6. NUnit Test Filtering

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

## 7. Allure Reports

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

## 8. CI/CD Pipeline Integration

CI pipeline files are maintained under `.github/workflows/` (GitHub Actions) and `ci/` (Azure Pipelines).

### GitHub Actions

| File | Trigger | Scope |
|------|---------|-------|
| [.github/workflows/ci.yml](../.github/workflows/ci.yml) | Push / PR to `main`, `develop` | PR → smoke tests; push → full suite |
| [.github/workflows/nightly.yml](../.github/workflows/nightly.yml) | Schedule 02:00 UTC + manual dispatch | Full suite (manual dispatch can select smoke) |

**Pipeline stages (`ci.yml`):**

```
build → api-tests ─────────────────────────────┐
      → ui-tests (matrix: chrome/firefox/edge) ─┴→ allure-report
```

**Required secrets** (set in GitHub → Settings → Secrets → Actions):

| Secret | Value |
|--------|-------|
| `TEST_USER_EMAIL` | Test account email |
| `TEST_USER_PASSWORD` | Test account password |

**PR vs Push behaviour:**
- Pull request → `--filter "Category=High&Category=Smoke"` applied to both API and UI jobs
- Push to `main`/`develop` → full suite with tiered browser filters applied

**Tiered browser filter (full suite runs):**

| Browser | Filter Applied | Tests Run |
|---------|----------------|-----------|
| Chrome | none | All tests |
| Firefox | `Category=High` | High priority tests |
| Edge | `Category=High&Category=Smoke` | Smoke tests only |

> This reduces Firefox/Edge execution time while Chrome provides complete regression coverage.

**Manual nightly dispatch** — trigger via GitHub Actions UI → Nightly → Run workflow → choose `full` or `smoke`.

---

### Azure Pipelines

Pipeline file: [ci/azure-pipelines.yml](../ci/azure-pipelines.yml)

Configure the pipeline in Azure DevOps:
1. **New pipeline** → connect your repo → select **Existing Azure Pipelines YAML file** → path: `ci/azure-pipelines.yml`
2. Add pipeline variables `TEST_USER_EMAIL` and `TEST_USER_PASSWORD` (mark both as **secret**)

**Pipeline stages:**

```
Build → Test (parallel: APITests + UITests matrix: chrome/firefox/edge) → Report
```

Same PR/push filtering logic as GitHub Actions: PRs run smoke tests, branch pushes run full suite.

---

### Recommended CI Settings

| Setting | Recommendation |
|---------|----------------|
| Headless | Always `true` in CI — set via `TestSettings__Headless` env var |
| Credentials | Use platform secrets — never hard-code |
| Build step | Restore and build before test; use `--no-build` in test step |
| Report | Allure results are uploaded per job and merged in the report step |
| PR filter | `Category=High&Category=Smoke` — fast smoke gate on every PR |
| Browser tier | Chrome runs all tests; Firefox runs `Category=High`; Edge runs `Category=High&Category=Smoke` |
| Nightly | Full suite with tiered browser filters for optimised cross-browser coverage |

---

## 9. What Is Implemented

This section summarizes what is already implemented for parallel execution and CI/CD.

### Parallel Test Execution (Implemented)

- NUnit parallel execution enabled at assembly level with `LevelOfParallelism(4)`.
- UI fixtures use `FixtureLifeCycle(LifeCycle.InstancePerTestCase)` + `Parallelizable(ParallelScope.All)` for safe method-level parallelism.
- API fixtures use `Parallelizable(ParallelScope.Self)` for class-level parallelism.
- UI and API suites can run in one command (`dotnet test SeleniumAutomationFramework.sln`) with concurrent fixture execution.
- Sequential debug mode is supported using `-- NUnit.NumberOfTestWorkers=0`.
- Cross-browser execution is supported by process-level browser selection via `TestSettings__Browser`.

### CI/CD (Implemented)

- GitHub Actions CI pipeline: `.github/workflows/ci.yml`.
- GitHub Actions nightly pipeline: `.github/workflows/nightly.yml`.
- Azure Pipelines definition: `ci/azure-pipelines.yml`.
- Cross-browser matrix execution in CI for Chrome, Firefox, and Edge with tiered browser filters.
- PR optimization: smoke-only filter (`Category=High&Category=Smoke`).
- Push/nightly execution: full suite with tiered browser strategy (Chrome=all, Firefox=High priority, Edge=Smoke only).
- Allure results are uploaded per test job and merged into a final report artifact.
- Manual trigger support exists through `workflow_dispatch`.

### CI Execution Flow (Implemented)

```text
Build -> API Tests + UI Tests (matrix browsers) -> Allure Report
```

### Required Configuration For CI

- GitHub repository secrets: `TEST_USER_EMAIL`, `TEST_USER_PASSWORD`.
- Azure pipeline secret variables: `TEST_USER_EMAIL`, `TEST_USER_PASSWORD`.
- Browser runs in CI should set `TestSettings__Headless=true`.

---

## 10. Troubleshooting

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
