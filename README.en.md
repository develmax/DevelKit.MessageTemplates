<img src="https://raw.githubusercontent.com/develmax/DevelKit.MessageTemplates/main/assets/logo.png" alt="DevelKit.MessageTemplates icon" width="96" height="96" />

# DevelKit.MessageTemplates

[Русский](https://github.com/develmax/DevelKit.MessageTemplates/blob/main/README.md) | **English**

A .NET message templating library with entity fields, relationships, conditions, .NET formatting, and named parameters. Use it to prepare SMS, email, push notifications, or other text messages. Your application handles delivery and channel-specific rules.

[![Version: 0.1.0](https://img.shields.io/badge/version-0.1.0-blue)](https://github.com/develmax/DevelKit.MessageTemplates/blob/main/Directory.Build.props)
[![Build and tests](https://github.com/develmax/DevelKit.MessageTemplates/actions/workflows/build.yml/badge.svg?branch=main)](https://github.com/develmax/DevelKit.MessageTemplates/actions/workflows/build.yml)
[![License: MIT](https://img.shields.io/github/license/develmax/DevelKit.MessageTemplates)](https://github.com/develmax/DevelKit.MessageTemplates/blob/main/LICENSE)
[![NuGet](https://img.shields.io/nuget/vpre/DevelKit.MessageTemplates)](https://www.nuget.org/packages/DevelKit.MessageTemplates)

[![.NET 10.0](https://img.shields.io/badge/.NET-10.0-512BD4)](https://github.com/develmax/DevelKit.MessageTemplates/blob/main/src/DevelKit.MessageTemplates/DevelKit.MessageTemplates.csproj)
[![.NET Framework 4.5.2](https://img.shields.io/badge/.NET_Framework-4.5.2-512BD4)](https://github.com/develmax/DevelKit.MessageTemplates/blob/main/docs/en/compatibility.md)
[![Dynamics CRM 2015](https://img.shields.io/badge/Dynamics_CRM-2015-0078D4)](https://github.com/develmax/DevelKit.MessageTemplates/blob/main/docs/en/dynamics-crm.md)
[![SQL Server](https://img.shields.io/badge/database-SQL_Server-CC2927)](https://github.com/develmax/DevelKit.MessageTemplates/tree/main/src/DevelKit.MessageTemplates.SqlServer)
[![Docs: EN / RU](https://img.shields.io/badge/docs-EN_%2F_RU-blue)](https://github.com/develmax/DevelKit.MessageTemplates/blob/main/README.en.md#documentation-and-samples)

## Packages

| Package | Target frameworks | Purpose |
| --- | --- | --- |
| [DevelKit.MessageTemplates](https://www.nuget.org/packages/DevelKit.MessageTemplates) | net452; net10.0 | Parser, formatter, parameter catalog, cache, and provider-independent query planning |
| [DevelKit.MessageTemplates.DynamicsCrm](https://www.nuget.org/packages/DevelKit.MessageTemplates.DynamicsCrm) | net452 | Dynamics CRM 2015 SDK 7.0.0.1 adapter |
| [DevelKit.MessageTemplates.SqlServer](https://www.nuget.org/packages/DevelKit.MessageTemplates.SqlServer) | net10.0 | SQL Server generation and parameterized DbCommand creation |

Each adapter depends on the core and can be installed separately. The core has no external runtime dependencies. Current version: **0.1.0**. Packages are available on NuGet.org using the links in the table above. Stable version 0.1.0 is prepared in source and local packages. Until it is published separately, NuGet.org provides 0.1.0-preview.3.

## Quick start without a database

```csharp
using DevelKit.MessageTemplates;

var template = TextTemplateParser.Parse(
    "Hello, {{person:name}}! Meeting: {{meeting:startsAt(dd.MM.yyyy HH:mm)}}.");

// Values can come from data your application has already loaded.
var text = TextTemplateFormatter.Format(template, parameter =>
    parameter.FieldName switch
    {
        "name" => "Anna",
        "startsAt" => new DateTime(2026, 9, 15, 14, 30, 0),
        _ => null
    });

Console.WriteLine(text);
// Hello, Anna! Meeting: 15.09.2026 14:30.
```

The parser and formatter work independently of the adapters. To load data based on a template, TemplateEngine builds a query plan and passes it to ITemplateDataProvider. Define allowed entities, fields, and relationships with TemplateSchema or your own IQueryMetadata.

## Template language

```text
Field: {{person:name}}
Format: {{meeting:startsAt(dd.MM.yyyy HH:mm)}}
Relationship: {{meeting:guest{{person:name}}}}
Condition: {{meeting:if[[isOnline = true ? meetingUrl ; address]]}}
```

Named parameters are expanded before parsing:

```csharp
var aliases = new[]
{
    new TemplateAlias("{{meeting:date}}", "{{meeting:startsAt(dd.MM.yyyy)}}")
};
var expanded = TemplateAliases.Expand("See you on {{meeting:date}}.", aliases);
```

Load the catalog through ITemplateAliasRepository and cache it with CachedTemplateAliases. Call Invalidate after changing the catalog. In this version, alias keys use {{entity:field}} syntax with no spaces inside either name.

## Build and verify

The full build requires Windows, PowerShell 7, .NET SDK 10, and .NET Framework 4.8 to run the net452 applications. Dependencies are restored from nuget.org.

```powershell
./build.ps1
```

The script builds the solution in Release, runs the core, SQL, and CRM checks, runs both samples, and creates three packages in artifacts. Tests and samples require no servers or credentials. GitHub Actions runs the same script on Windows.

## Documentation and samples

- [Syntax and processing](https://github.com/develmax/DevelKit.MessageTemplates/blob/main/docs/en/syntax.md).
- [Code guide](https://github.com/develmax/DevelKit.MessageTemplates/blob/main/docs/en/code-guide.md).
- [Behavior and limitations](https://github.com/develmax/DevelKit.MessageTemplates/blob/main/docs/en/compatibility.md).
- [CRM adapter](https://github.com/develmax/DevelKit.MessageTemplates/blob/main/docs/en/dynamics-crm.md).
- [SQL sample](https://github.com/develmax/DevelKit.MessageTemplates/blob/main/samples/Meetings/Program.cs) and [CRM SDK sample](https://github.com/develmax/DevelKit.MessageTemplates/blob/main/samples/CrmMeetings/Program.cs).
- [Building and releasing packages](https://github.com/develmax/DevelKit.MessageTemplates/blob/main/docs/en/release.md).
- [Contributing](https://github.com/develmax/DevelKit.MessageTemplates/blob/main/CONTRIBUTING.en.md).

Complete SQL scenario: [NotificationFlow](samples/NotificationFlow/README.en.md). [Preparing types and time values before formatting](docs/en/value-preparation.md).

## Scope and limitations

Templates access data within the schema allowed by your application. SQL identifier validation does not replace access control. SQL values are passed through DbParameter. Both branches of an if expression are loaded before the formatter selects a result.

Parsing is permissive: unrecognized fragments may remain literal text. Your application supplies HTML encoding, length limits, SMS cost rules, and time zone handling. Query plans return a single row and do not support collection loops. Core regression checks require no servers. The separate NotificationFlow sample executes SQL Server queries and verifies rendered messages. Live CRM, the CRM plugin sandbox, and modern Dataverse have not been validated.

## Support development

If this library helps you, you can support its maintenance, tests, and documentation.
Donations are optional; the library remains freely available under the MIT license.

[Patreon](https://www.patreon.com/develmax) · [Boosty](https://boosty.to/develmax/donate) · [YooMoney](https://yoomoney.ru/to/4100119529133322) · [PayPal](https://paypal.me/develmax)

## License

[MIT](https://github.com/develmax/DevelKit.MessageTemplates/blob/main/LICENSE).
Copyright (c) 2026 Maksim Moiseev (develmax).

Modification and commercial use are permitted. Retain the copyright notice and license text when distributing the software.
