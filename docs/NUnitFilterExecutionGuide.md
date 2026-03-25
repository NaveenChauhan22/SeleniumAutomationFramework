# NUnit Filter Execution Guide

This framework now includes NUnit-native categories on tests so they can be filtered from CLI and CI.

## Priority to Category Mapping

- `TestPriority.High` -> `Category("High")` + `Category("Smoke")`
- `TestPriority.Medium` -> `Category("High")` + `Category("Smoke")`
- `TestPriority.Low` -> `Category("Medium")` + `Category("Sanity")`

## Run by Test Level (single test)

**Windows (PowerShell):**

```powershell
dotnet test .\tests\UITests\UITests.csproj --filter "Name=Login_WithValidCredentials"
dotnet test .\tests\APITests\APITests.csproj --filter "Name=MeEndpoint_WithValidToken_ShouldReturnCurrentUser"
```

**macOS / Linux (zsh/bash):**

```bash
dotnet test ./tests/UITests/UITests.csproj --filter "Name=Login_WithValidCredentials"
dotnet test ./tests/APITests/APITests.csproj --filter "Name=MeEndpoint_WithValidToken_ShouldReturnCurrentUser"
```

Alternative using fully-qualified name contains:

**Windows (PowerShell):**

```powershell
dotnet test .\tests\UITests\UITests.csproj --filter "FullyQualifiedName~Login_WithValidCredentials"
dotnet test .\tests\APITests\APITests.csproj --filter "FullyQualifiedName~MeEndpoint_WithValidToken_ShouldReturnCurrentUser"
```

**macOS / Linux (zsh/bash):**

```bash
dotnet test ./tests/UITests/UITests.csproj --filter "FullyQualifiedName~Login_WithValidCredentials"
dotnet test ./tests/APITests/APITests.csproj --filter "FullyQualifiedName~MeEndpoint_WithValidToken_ShouldReturnCurrentUser"
```

## Run by Suite Level (UI or API project scope)

Run all High category tests only within one suite:

**Windows (PowerShell):**

```powershell
dotnet test .\tests\UITests\UITests.csproj --filter "Category=High"
dotnet test .\tests\APITests\APITests.csproj --filter "Category=High"
```

**macOS / Linux (zsh/bash):**

```bash
dotnet test ./tests/UITests/UITests.csproj --filter "Category=High"
dotnet test ./tests/APITests/APITests.csproj --filter "Category=High"
```

Run only Sanity tests (currently mapped from `TestPriority.Low`):

**Windows (PowerShell):**

```powershell
dotnet test .\tests\UITests\UITests.csproj --filter "Category=Sanity"
dotnet test .\tests\APITests\APITests.csproj --filter "Category=Sanity"
```

**macOS / Linux (zsh/bash):**

```bash
dotnet test ./tests/UITests/UITests.csproj --filter "Category=Sanity"
dotnet test ./tests/APITests/APITests.csproj --filter "Category=Sanity"
```

Run tests that must have both categories:

**Windows (PowerShell):**

```powershell
dotnet test .\tests\UITests\UITests.csproj --filter "Category=High&Category=Smoke"
dotnet test .\tests\APITests\APITests.csproj --filter "Category=High&Category=Smoke"
```

**macOS / Linux (zsh/bash):**

```bash
dotnet test ./tests/UITests/UITests.csproj --filter "Category=High&Category=Smoke"
dotnet test ./tests/APITests/APITests.csproj --filter "Category=High&Category=Smoke"
```

## Run by Framework Level (whole solution scope)

From repository root, run filtered tests across both API and UI projects:

**Windows (PowerShell):**

```powershell
dotnet test .\SeleniumAutomationFramework.sln --filter "Category=High"
dotnet test .\SeleniumAutomationFramework.sln --filter "Category=High&Category=Smoke"
dotnet test .\SeleniumAutomationFramework.sln --filter "Category=Medium&Category=Sanity"
```

**macOS / Linux (zsh/bash):**

```bash
dotnet test ./SeleniumAutomationFramework.sln --filter "Category=High"
dotnet test ./SeleniumAutomationFramework.sln --filter "Category=High&Category=Smoke"
dotnet test ./SeleniumAutomationFramework.sln --filter "Category=Medium&Category=Sanity"
```

## Useful Composite Filters

High/Smoke only:

**Windows (PowerShell):**

```powershell
dotnet test .\SeleniumAutomationFramework.sln --filter "Category=High&Category=Smoke"
```

**macOS / Linux (zsh/bash):**

```bash
dotnet test ./SeleniumAutomationFramework.sln --filter "Category=High&Category=Smoke"
```

Exclude Sanity tests:

**Windows (PowerShell):**

```powershell
dotnet test .\SeleniumAutomationFramework.sln --filter "Category!=Sanity"
```

**macOS / Linux (zsh/bash):**

```bash
dotnet test ./SeleniumAutomationFramework.sln --filter "Category!=Sanity"
```

Run only UI login tests with High category:

**Windows (PowerShell):**

```powershell
dotnet test .\tests\UITests\UITests.csproj --filter "FullyQualifiedName~Login&Category=High"
```

**macOS / Linux (zsh/bash):**

```bash
dotnet test ./tests/UITests/UITests.csproj --filter "FullyQualifiedName~Login&Category=High"
```

## CI Example

**Windows (PowerShell):**

```powershell
dotnet restore
dotnet build --no-restore
dotnet test .\SeleniumAutomationFramework.sln --no-build --filter "Category=High&Category=Smoke"
```

**macOS / Linux (zsh/bash):**

```bash
dotnet restore
dotnet build --no-restore
dotnet test ./SeleniumAutomationFramework.sln --no-build --filter "Category=High&Category=Smoke"
```

## Verify discovered tests and names

**Windows (PowerShell):**

```powershell
dotnet test .\tests\UITests\UITests.csproj --list-tests
dotnet test .\tests\APITests\APITests.csproj --list-tests
```

**macOS / Linux (zsh/bash):**

```bash
dotnet test ./tests/UITests/UITests.csproj --list-tests
dotnet test ./tests/APITests/APITests.csproj --list-tests
```
