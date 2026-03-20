# Secure Test Credentials Setup Guide

## Overview

Test credentials in `resources/testdata/loginData.json` are now protected using **environment variable substitution**. Sensitive credentials are referenced via `${VARIABLE_NAME}` syntax instead of being hardcoded in the JSON file.

This ensures:
- ✅ Credentials are **never exposed** in source control
- ✅ Different credentials can be used per environment (local, CI/CD, staging, production)
- ✅ Clear error messages guide setup when credentials are missing
- ✅ Existing tests continue to work without modification

---

## Required Environment Variables for Valid Credentials

| Variable | Purpose | Example |
|----------|---------|---------|
| `TEST_USER_EMAIL` | Valid test account email | `correctemail@example.com` |
| `TEST_USER_PASSWORD` | Valid test account password | `correctpassword` |

Other test scenarios (invalid email, wrong password, unregistered email) use hardcoded test data values and **do not** require environment variables.

---

## How It Works

### JSON File Reference

**resources/testdata/loginData.json:**
```json
{
  "validCredentials": {
    "email": "${TEST_USER_EMAIL}",
    "password": "${TEST_USER_PASSWORD}"
  }
}
```

### Substitution Process

1. **Load JSON file** → `resources/testdata/loginData.json`
2. **Find placeholders** → Regex pattern `${VARIABLE_NAME}`
3. **Replace with environment values** → `Environment.GetEnvironmentVariable()`
4. **Deserialize to model** → `JsonConvert.DeserializeObject<T>()`

This is handled automatically by `Framework.Data.JsonDataProvider.cs`.

---

## How to Set Environment Variables

### Option 1: PowerShell (Session-based)

Set variables for the current PowerShell session:

```powershell
$env:TEST_USER_EMAIL = "correctemail@example.com"
$env:TEST_USER_PASSWORD = "correctpassword"

# Then run tests
dotnet test tests/UITests/UITests.csproj
```

### Option 2: Command Prompt (Session-based)

```cmd
set TEST_USER_EMAIL=correctemail@example.com
set TEST_USER_PASSWORD=correctpassword

dotnet test tests/UITests/UITests.csproj
```

### Option 3: .env File (Local Development - Recommended)

Create a `.env` file in the project root with your credentials:

**Step 1:** Copy the template
```bash
copy .env.example .env
```

**Step 2:** Edit `.env` and add your credentials
```
TEST_USER_EMAIL=correctemail@example.com
TEST_USER_PASSWORD=correctpassword
```

**Step 3:** Load environment variables before running tests

**Method A: Using the provided helper script (Recommended)**

```powershell
# Run the helper script from project root
. ./Load-Env.ps1

# Now run tests
dotnet test tests/UITests/UITests.csproj
```

**Method B: Manual loading in PowerShell**

```powershell
# Load .env file into current session (PowerShell)
Get-Content .env | ForEach-Object {
    if ($_ -and !$_.StartsWith('#')) {
        $name, $value = $_.Split('=', 2)
        [Environment]::SetEnvironmentVariable($name, $value)
    }
}

# Now run tests
dotnet test tests/UITests/UITests.csproj
```

**Method C: Using a helper script function** (Not implemented yet)

```powershell
# Define a reusable function
function Load-EnvFile {
    param([string]$Path = ".env")
    if (Test-Path $Path) {
        Get-Content $Path | ForEach-Object {
            if ($_ -and !$_.StartsWith('#')) {
                $parts = $_ -split '=', 2
                if ($parts.Count -eq 2) {
                    [Environment]::SetEnvironmentVariable($parts[0].Trim(), $parts[1].Trim())
                }
            }
        }
        Write-Host "Loaded environment variables from $Path"
    }
}

# Use it
Load-EnvFile
dotnet test tests/UITests/UITests.csproj
```

**Important Security Notes:**
- ✅ `.env` is in `.gitignore` — it will **never** be committed to version control
- ✅ `.env.example` is provided as a template showing the structure
- ✅ Each developer has their own `.env` with their credentials
- ❌ Never commit your actual `.env` file with real credentials
- ❌ Don't share `.env` file contents via email or chat

### Option 4: Windows Permanent Environment Variables

1. Press `Win + Pause` → **Environment Variables**
2. Click **New** under "User variables"
3. Add `TEST_USER_EMAIL` and `TEST_USER_PASSWORD`
4. Click **OK** and restart terminal/IDE

**Or using PowerShell (Admin):**
```powershell
[Environment]::SetEnvironmentVariable("TEST_USER_EMAIL", "correctemail@example.com", "User")
[Environment]::SetEnvironmentVariable("TEST_USER_PASSWORD", "correctpassword", "User")
```

### Option 5: CI/CD Pipeline
```yaml
env:
  TEST_USER_EMAIL: ${{ secrets.TEST_USER_EMAIL }}
  TEST_USER_PASSWORD: ${{ secrets.TEST_USER_PASSWORD }}

jobs:
  test:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v3
      - name: Run Tests
        run: dotnet test tests/UITests/UITests.csproj
```

#### Azure Pipelines
```yaml
variables:
  - group: 'Test Credentials'  # Contains TEST_USER_EMAIL and TEST_USER_PASSWORD

jobs:
  - job: UITests
    pool:
      vmImage: 'windows-latest'
    steps:
      - task: DotNetCoreCLI@2
        inputs:
          command: 'test'
          projects: 'tests/UITests/UITests.csproj'
```

---

## Error Handling

### When Environment Variables Are Missing

If `TEST_USER_EMAIL` or `TEST_USER_PASSWORD` are not set, tests fail with a clear error message:

```
System.InvalidOperationException: Environment variable 'TEST_USER_EMAIL' not found and no default value provided.
Please set the environment variable 'TEST_USER_EMAIL' before running tests.
For validCredentials in loginData.json, use: TEST_USER_EMAIL, TEST_USER_PASSWORD
```

**To fix:**
1. Set the missing environment variable
2. Restart your terminal/IDE
3. Re-run the tests

### Verify Environment Variables Are Set

**PowerShell:**
```powershell
$env:TEST_USER_EMAIL
$env:TEST_USER_PASSWORD
```

**Command Prompt:**
```cmd
echo %TEST_USER_EMAIL%
echo %TEST_USER_PASSWORD%
```

---

## Test Data File Structure

### Valid Credentials (Environment Variables)
```json
{
  "validCredentials": {
    "email": "${TEST_USER_EMAIL}",
    "password": "${TEST_USER_PASSWORD}"
  }
}
```

Only **validCredentials** use environment variables. Other scenarios use hardcoded test values.

---

## Implementation Details

### JsonDataProvider.cs

The `JsonDataProvider.Read<T>()` method automatically:
1. Loads JSON file from disk
2. Scans for `${VARIABLE_NAME}` patterns
3. Replaces with environment variable values
4. Throws `InvalidOperationException` if variable not found
5. Deserializes to the specified type

Supported syntax:
- `${VAR_NAME}` — Required variable, throws if missing
- `${VAR_NAME:-default}` — Optional variable with fallback (currently unused)

### BaseTest.cs

The `LoadLoginData()` method:
1. Loads `loginData.json` (environment variables substituted automatically)
2. Validates that `ValidCredentials.Email` and `ValidCredentials.Password` are populated
3. Returns assertion messages indicating these come from environment variables
4. Other scenarios are validated as before

---

## Best Practices

✅ **DO:**
- Set environment variables before running UI tests
- Use environment variables for all accounts (dev, staging, prod)
- Document required variables in your CI/CD pipeline
- Use platform-specific secret management (GitHub Secrets, Azure Key Vault, etc.)

❌ **DON'T:**
- Hardcode actual credentials in JSON files
- Commit `.env` files or credential files to source control
- Share environment variable values via chat or email
- Use the same credentials for multiple environments

---

## Troubleshooting

| Issue | Solution |
|-------|----------|
| `TEST_USER_EMAIL not found` | Set `$env:TEST_USER_EMAIL = "value"` and restart terminal |
| Tests pass locally but fail in CI/CD | Verify secrets are configured in GitHub/Azure platform |
| Old hardcoded credentials still appear | Clear browser cache or check JSON file for leftover values |
| Variable value contains special characters | Ensure proper escaping in environment variable setup |

---

## Related Files

- **JSON Data Provider**: [src/Framework.Data/JsonDataProvider.cs](../src/Framework.Data/JsonDataProvider.cs)
- **Test Data**: [resources/testdata/loginData.json](../resources/testdata/loginData.json)
- **Base Test Class**: [tests/UITests/BaseTest.cs](../tests/UITests/BaseTest.cs)
- **Login Tests**: [tests/UITests/LoginTest.cs](../tests/UITests/LoginTest.cs)


## Note
Credentials mentioned above are just for reference purpose and are not correct. 
