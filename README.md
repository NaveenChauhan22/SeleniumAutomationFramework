
# Selenium Automation Framework (C#)

A reusable **enterprise-grade Selenium automation framework** built using **C#, .NET, and NUnit**.
This framework is designed to support scalable **UI and API automation** across multiple web applications.

---

## Key Features

- Selenium WebDriver based UI automation
- Page Object Model (POM) architecture
- API automation support
- Data-driven testing (Excel / JSON)
- Cross-browser execution
- Parallel test execution
- Structured reporting using **Allure**
- CI/CD integration with **GitHub Actions**
- Modular and extensible framework design

---

## Architecture

The framework follows a **layered enterprise architecture**.

Test Layer  
↓  
Page Objects  
↓  
Framework Core Utilities  
↓  
Selenium WebDriver  
↓  
Application Under Test

For full architecture details see:

docs/FrameworkArchitecture.md

---

## Repository Structure

SeleniumAutomationFramework
│
├── src
│   ├── Framework.Core
│   ├── Framework.API
│   ├── Framework.Data
│   └── Framework.Reporting
│
├── tests
│   ├── UITests
│   └── APITests
│
├── config
├── resources
├── reports
├── ci
└── docs

Folder purposes:

src → Core automation framework components  
tests → UI and API test implementations  
config → Environment configuration files  
resources → Test data and payloads  
reports → Generated execution reports  
ci → CI/CD pipeline configuration  
docs → Architecture and documentation  

---

## Technology Stack

Language: C# (.NET)  
UI Automation: Selenium WebDriver  
Test Framework: NUnit  
API Testing: HttpClient  
Assertions: FluentAssertions  
Logging: Serilog  
Reporting: Allure Reports  
Driver Management: WebDriverManager  
IDE: Visual Studio Code  
CI/CD: GitHub Actions  

---

## Development Environment

Recommended setup:

- .NET SDK
- Visual Studio Code
- Git

Recommended VS Code extensions:

- C#
- C# Dev Kit
- GitHub Copilot
- GitHub Copilot Chat
- .NET Test Explorer

Detailed setup instructions are available in:

docs/FrameworkArchitecture.md

---

## Running Tests

From the project root:

dotnet test

---

## 🔧 CI/CD

The framework supports automated execution through **GitHub Actions**.

Automation pipelines will:

- Run tests on pull requests
- Execute scheduled regression runs
- Publish Allure test reports

Pipeline configurations are located in:

ci/

---

## Contributors

Naveen Chauhan — Framework Architecture  
Aman — Framework Implementation  

---

## Roadmap

Phase 1
- Core framework setup
- Selenium integration
- Page Object Model
- Reporting
- CI/CD pipeline

Phase 2
- Selenium Grid execution
- Docker support
- BDD integration
- Visual testing
- Mobile automation

---

## License

Internal framework for QA automation experimentation and learning.
