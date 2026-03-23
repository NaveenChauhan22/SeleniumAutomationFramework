# Allure Reporting

This framework keeps the existing HTML report under `reports` and also writes Allure artifacts to:

- `reports/allure-results`
- `reports/allure-report`

## Centralized Setup

Allure integration is centralized so UI and API test projects do not maintain duplicate files:

- Shared Allure configuration file: `config/allureConfig.json`
- Shared NUnit setup fixture source: `src/Framework.Reporting/AllureHooks.cs`

Both test projects link these shared files at build time.

## Prerequisites

1. Ensure the Allure CLI is installed and available on `PATH`, or set `ALLURE_HOME`.
2. Keep Java installed if your Allure CLI distribution requires it.

## Run The Tests

Run the API suite:

```powershell
dotnet test .\tests\APITests\APITests.csproj
```

```bash
dotnet test ./tests/APITests/APITests.csproj
```

Run the UI suite:

```powershell
dotnet test .\tests\UITests\UITests.csproj
```

```bash
dotnet test ./tests/UITests/UITests.csproj
```

Run with a browser override:

```powershell
$env:TestSettings__Browser = "chrome"
dotnet test .\tests\UITests\UITests.csproj
```

```bash
export TestSettings__Browser="chrome"
dotnet test ./tests/UITests/UITests.csproj
```

## Set Test Priority

Apply the reusable priority attribute at class or method level:

```csharp
[Priority(TestPriority.High)]
[Test]
public void Login_WithValidCredentials()
{
}
```

Priority mapping in Allure:

- `High` -> `critical` severity and `priority=High`
- `Medium` -> `normal` severity and `priority=Medium`
- `Low` -> `minor` severity and `priority=Low`

## Open The Allure Report

The framework attempts to generate `reports/allure-report` automatically at the end of a run when the Allure CLI is available.

Open the generated report with:

```powershell
allure open .\reports\allure-report
```

```bash
allure open ./reports/allure-report
```

If you need to regenerate it manually:

```powershell
allure generate .\reports\allure-results -o .\reports\allure-report --clean
```

```bash
allure generate ./reports/allure-results -o ./reports/allure-report --clean
```

## Failure Attachments

For failed UI tests:

- Screenshot file is saved in `reports/screenshots` with a unique filename
- Screenshot is also attached to Allure
- Page source is attached to Allure
- HTML execution report links to the screenshot saved under `reports/screenshots`

For failed API tests:

- Request and response payloads are attached to Allure when available

## Report Contents

The Allure report includes:

- Overview totals for passed, failed, skipped, and broken tests
- Suite hierarchy on the Suites tab
- Environment details for browser, OS, framework, execution start, execution end, duration, and test type
- Severity grouping based on test priority
- Failure categories for assertions, timeouts, missing elements, and HTTP 5xx issues
- Failure attachments such as screenshots and page source for UI failures
- Request and response attachments for API failures when available

