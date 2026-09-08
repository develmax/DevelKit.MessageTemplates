# Contributing

[Русский](CONTRIBUTING.md) | **English**

Create a branch and submit a pull request describing the problem, intended behavior, and validation performed. Use GitHub Issues for public discussions.

Before submitting code, run ./build.ps1 on Windows with PowerShell 7, .NET SDK 10, and .NET Framework 4.8. For a behavior fix, add a reproducing scenario to tests/Regression or tests/DynamicsCrm.Regression. For documentation changes, verify examples and links.

The core targets both net452 and net10.0. Preserve compatibility with both. The CRM adapter uses the 2015 SDK, and the SQL adapter depends only on the core. Do not include organization-specific schemas, infrastructure addresses, credentials, or real customer data.

When changing the grammar, keep the parser, tree, formatter, query planning, and docs/en/syntax.md consistent. Even a small change in null handling or numeric comparisons can affect existing messages.

Keep the English and Russian documentation in sync when changing documented behavior. Code examples should demonstrate the same API and limitations in both languages.

The project is licensed under MIT. Contributions must preserve those terms and existing copyright notices.
