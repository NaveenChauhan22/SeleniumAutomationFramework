# Setup Guide (Windows + macOS)

Use this guide if you are setting up the framework for the first time.

---

## What You Will Achieve

By the end of this guide, you will be able to:
- Install all required tools in the correct order.
- Clone and open the framework project.
- Configure test credentials safely.
- Run a first successful test execution.

---

## 1. Install Prerequisites in Sequence

Follow this exact order. Do not skip verification commands.

### Step 1: Install Visual Studio Code (IDE)

- Download and install VS Code: https://code.visualstudio.com/

Why this step: VS Code is the editor used to open, run, and troubleshoot this framework.

### Step 2: Install Git

- Download and install Git: https://git-scm.com/downloads

Verify installation:

**Windows (PowerShell):**
```powershell
git --version
```

**macOS (Terminal):**
```bash
git --version
```

Why this step: Git lets you download the repository and keep your local code updated.

### Step 3: Install .NET SDK (Required)

- Install .NET SDK 10.x from: https://dotnet.microsoft.com/download

Verify installation:

**Windows (PowerShell):**
```powershell
dotnet --version
dotnet --list-sdks
```

**macOS (Terminal):**
```bash
dotnet --version
dotnet --list-sdks
```

Why this step: The test framework is built on .NET and cannot run without the SDK.

### Step 4: Install Java (Required Before Allure CLI)

- Install Java 8+ (recommended: Temurin 17): https://adoptium.net/

Verify installation:

**Windows (PowerShell):**
```powershell
java -version
```

**macOS (Terminal):**
```bash
java -version
```

Why this step: Allure CLI depends on Java, so Java must be installed first.

### Step 5: Install a Browser for UI Tests

- Install at least one: Chrome, Edge, or Firefox (latest stable version).

Why this step: UI tests open a real browser to perform actions like login and navigation.

### Step 6: Install Allure CLI (After Java)

**Windows option 1 (Scoop):**
```powershell
scoop install allure
```

**Windows option 2 (Chocolatey):**
```powershell
choco install allure
```

**macOS (Homebrew):**
```bash
brew install allure
```

Verify installation:

**Windows (PowerShell):**
```powershell
allure --version
```

**macOS (Terminal):**
```bash
allure --version
```

Why this step: Allure CLI generates and opens the HTML test report.

### Step 7: Install Useful VS Code Extensions (Recommended)

- C#
- C# Dev Kit
- .NET Test Explorer

Why this step: These extensions make test execution and troubleshooting easier for beginners.

---

## 2. Repository Setup

### Step 1: Clone the Repository

**Windows (PowerShell):**
```powershell
git clone <repository-url>
cd SeleniumAutomationFramework
```

**macOS (Terminal):**
```bash
git clone <repository-url>
cd SeleniumAutomationFramework
```

Why this step: This creates your local project copy where you will run all commands.

### Step 2: Open the Project Folder in VS Code

**Windows (PowerShell):**
```powershell
code .
```

**macOS (Terminal):**
```bash
code .
```

Why this step: Opening the folder in VS Code lets you run tests and view reports from one place.

### Step 3: Restore Project Dependencies

**Windows (PowerShell):**
```powershell
dotnet restore
```

**macOS (Terminal):**
```bash
dotnet restore
```

Why this step: This downloads all required NuGet packages before build and test execution.

### Step 4: Build the Solution

**Windows (PowerShell):**
```powershell
dotnet build .\SeleniumAutomationFramework.sln
```

**macOS (Terminal):**
```bash
dotnet build ./SeleniumAutomationFramework.sln
```

Why this step: Build confirms your environment is correct and the code compiles successfully.

---

## 3. Configure Test Credentials

Use this section to set login credentials required by test data.

### Step 1: Create a Local .env File

**Windows (PowerShell):**
```powershell
Copy-Item .env.example .env
```

**macOS (Terminal):**
```bash
cp .env.example .env
```

Why this step: .env stores your local credentials without changing source code.

### Step 2: Add Your Test Credentials in .env

Update the file with your valid test account values:

```bash
TEST_USER_EMAIL=your_test_email@example.com
TEST_USER_PASSWORD=your_test_password
```

Why this step: Tests read these values at runtime to log in.

### Step 3: Understand Security Rule

- Never commit .env to source control.
- Keep only test credentials, never production credentials.

Why this step: This prevents accidental credential exposure.

### Step 4: Optional Quick Check for Session Variables

If you use terminal-based environment variables instead of .env:

**Windows (PowerShell):**
```powershell
$env:TEST_USER_EMAIL = "your_test_email@example.com"
$env:TEST_USER_PASSWORD = "your_test_password"
```

**macOS (Terminal):**
```bash
export TEST_USER_EMAIL="your_test_email@example.com"
export TEST_USER_PASSWORD="your_test_password"
```

Why this step: This gives an alternate way to inject credentials for a single terminal session.

---

## 4. Configure Browser and Headless Mode

Default browser is Chrome if no override is given.

### Step 1: Keep Default Browser (No Change Needed)

- Do nothing to use Chrome.

Why this step: This is the fastest path for first execution.

### Step 2: Change Browser in .env (Recommended Method)

Add or update in .env:

```bash
TestSettings__Browser=edge
TestSettings__Headless=false
```

Allowed browser values:
- chrome
- edge
- firefox

Why this step: .env is simple and works the same for both Windows and macOS.

### Step 3: Optional Terminal Override (Current Session Only)

**Windows (PowerShell):**
```powershell
$env:TestSettings__Browser = "firefox"
$env:TestSettings__Headless = "true"
```

**macOS (Terminal):**
```bash
export TestSettings__Browser="firefox"
export TestSettings__Headless="true"
```

Why this step: Useful for quick trial runs without editing files.

### Step 4: Change Permanent Defaults in appsettings.json

Open [config/appsettings.json](../config/appsettings.json) in VS Code and edit the values directly:

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

Changes here become the new default for every run on your machine, without needing to set environment variables or edit `.env`.

Why this step: Use `appsettings.json` when you want a setting to persist across all terminal sessions and IDE restarts — for example, always defaulting to Edge or increasing wait times for a slow environment. Full reference of every setting is in [ExecutionGuide.md — Section 8](./ExecutionGuide.md#8-useful-configuration-settings).

---

## 5. First Setup Validation Run

Use this section to confirm setup is complete.

### Step 1: Run All Tests Once

**Windows (PowerShell):**
```powershell
dotnet test .\SeleniumAutomationFramework.sln
```

**macOS (Terminal):**
```bash
dotnet test ./SeleniumAutomationFramework.sln
```

Why this step: A full run validates tools, credentials, browser config, and framework wiring in one go.

### Step 2: Generate Allure HTML Report

**Windows (PowerShell):**
```powershell
allure generate .\reports\allure-results -o .\reports\allure-report --clean
```

**macOS (Terminal):**
```bash
allure generate ./reports/allure-results -o ./reports/allure-report --clean
```

Why this step: This converts raw test result files into a readable report.

### Step 3: Open Allure Report

**Windows (PowerShell):**
```powershell
allure open .\reports\allure-report
```

**macOS (Terminal):**
```bash
allure open ./reports/allure-report
```

Why this step: You can quickly review pass/fail details and evidence such as screenshots.

---

## 6. Quick Troubleshooting

| Problem | What to Check |
|---------|---------------|
| `dotnet` command not found | Reinstall .NET SDK and reopen terminal |
| `allure` command not found | Install Allure CLI, then verify PATH |
| `java` command not found | Install Java first, then reinstall/verify Allure |
| Login test fails with missing credentials | Confirm `.env` exists and has `TEST_USER_EMAIL` and `TEST_USER_PASSWORD` |
| Wrong browser opens | Check `TestSettings__Browser` in `.env` and terminal overrides |

---

## 7. Next Document to Follow

After setup is complete, continue with execution steps in [ExecutionGuide.md](./ExecutionGuide.md).
