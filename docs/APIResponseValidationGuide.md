# API Response Validation Guide

## Purpose
This document is the single reference for response validation architecture, JSON/XML support, and test coverage in the API suite.

## Scope
- Fluent validation entry point: [src/Framework.API/ResponseValidator.cs](src/Framework.API/ResponseValidator.cs)
- Format detection logic (inlined): [src/Framework.API/ResponseValidator.cs](src/Framework.API/ResponseValidator.cs)
- Validator contract: [src/Framework.API/IResponseValidator.cs](src/Framework.API/IResponseValidator.cs)
- JSON implementation: [src/Framework.API/JsonResponseValidator.cs](src/Framework.API/JsonResponseValidator.cs)
- XML implementation: [src/Framework.API/XmlResponseValidator.cs](src/Framework.API/XmlResponseValidator.cs)

## Architecture
- `ResponseValidator` provides fluent chaining.
- `ResponseValidator` also performs format detection and validator selection (JSON/XML).
- `IResponseValidator` standardizes methods across formats.

## Supported Validation Methods
- `Validate(path, value)`
- `ValidateFieldExists(path)`
- `ValidateFieldNotExists(path)`
- `ValidateType(path, type)`
- `ValidateContains(path, substring)`
- `GetRawContent()`
- `GetContentType()`

## Format Detection Rules
1. Prefer response content-type header.
2. Fallback to payload shape:
- `{` or `[` => JSON
- `<` => XML
3. Default to JSON when ambiguous.

## JSON Validation Usage in Current API Tests
JSON validation is actively used in:
- [tests/APITests/AuthAPITests.cs](tests/APITests/AuthAPITests.cs)
- [tests/APITests/BookingsAPITests.cs](tests/APITests/BookingsAPITests.cs)
- [tests/APITests/EventsAPITests.cs](tests/APITests/EventsAPITests.cs)

Typical usage pattern:
```csharp
ResponseValidator
    .FromContent(response.ResponseBody)
    .ValidateFieldExists("data.id")
    .ValidateType("data.id", typeof(int));
```

## XML Validation Test Coverage
XML support is validated via dedicated sample tests in:
- [tests/APITests/XmlResponseValidatorTests.cs](tests/APITests/XmlResponseValidatorTests.cs)

XML payloads are loaded from resource files instead of inline test strings:
- [resources/testdata/xml/xml-core-assertions-response.xml](resources/testdata/xml/xml-core-assertions-response.xml)
- [resources/testdata/xml/xml-complex-paths-response.xml](resources/testdata/xml/xml-complex-paths-response.xml)
- [resources/testdata/xml/xml-error-shape-response.xml](resources/testdata/xml/xml-error-shape-response.xml)

Resource copying to runtime output is configured in:
- [tests/APITests/APITests.csproj](tests/APITests/APITests.csproj)

Current XML sample set is intentionally compact (3 tests) with detailed report logging:
1. Core assertions
2. Complex paths and types
3. Auto-detection and error shape

Each XML test attaches to report:
- XML payload
- Validation plan
- Validation result

## Current Coverage Snapshot
- JSON validation tests: existing API tests continue to validate response structure/content.
- XML validation tests: 3 focused tests to verify format support and validator behavior.

## Best Practices
- Use `ResponseValidator.FromContent(...)` for API result assertions.
- Keep path assertions explicit and stable.
- Prefer `ValidateType(...)` for IDs/counts and schema-sensitive fields.
- Add XML tests only where endpoints or adapters actually produce XML.

## Related Files
- [src/Framework.API/ResponseValidator.cs](src/Framework.API/ResponseValidator.cs)
- [src/Framework.API/IResponseValidator.cs](src/Framework.API/IResponseValidator.cs)
- [src/Framework.API/JsonResponseValidator.cs](src/Framework.API/JsonResponseValidator.cs)
- [src/Framework.API/XmlResponseValidator.cs](src/Framework.API/XmlResponseValidator.cs)
- [tests/APITests/XmlResponseValidatorTests.cs](tests/APITests/XmlResponseValidatorTests.cs)
- [resources/testdata/xml/xml-core-assertions-response.xml](resources/testdata/xml/xml-core-assertions-response.xml)
- [resources/testdata/xml/xml-complex-paths-response.xml](resources/testdata/xml/xml-complex-paths-response.xml)
- [resources/testdata/xml/xml-error-shape-response.xml](resources/testdata/xml/xml-error-shape-response.xml)
