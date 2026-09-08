# Building and releasing

[Русский](../release.md) | **English**

## Local verification

On Windows, install PowerShell 7, .NET SDK 10, and .NET Framework 4.8, then run from the repository root:

```powershell
./build.ps1
```

The script restores dependencies, builds all target frameworks, runs console checks for the core/SQL and CRM adapters, runs both samples, and creates packages in artifacts. It uses a demonstration IOrganizationService and in-memory data. No live servers are required.

Checks run through dotnet run and net452 executables. These are console scenarios, so dotnet test alone is not sufficient. GitHub Actions runs the same build.ps1 and saves nupkg files as workflow artifacts.

## Local NuGet feed

After building, register the absolute artifacts path in your consuming project:

```powershell
dotnet nuget add source "<absolute-path-to-artifacts>" --name DevelKitLocal
dotnet add package DevelKit.MessageTemplates.SqlServer --version 0.1.0-preview.3
```

For CRM, use DevelKit.MessageTemplates.DynamicsCrm in a net452 project. The core is a transitive dependency. Access to nuget.org is required to restore Microsoft.CrmSdk.CoreAssemblies.

## Preparing a new version

1. Update Version in Directory.Build.props and the version references in the documentation.
2. Run build.ps1.
3. Inspect all three nupkg files for DLLs, XML documentation, README, LICENSE, author, MIT, and RepositoryUrl.
4. Verify a package from a separate project using PackageReference.

Building or pushing source code does not publish packages to NuGet. Publication is a separate step performed by the repository owner after checking the version and PackageId ownership. Never commit API keys to source files or NuGet.Config.

## Metadata

All three packages use the MIT license, author Maksim Moiseev (develmax), and repository URL https://github.com/develmax/DevelKit.MessageTemplates. The CRM SDK version is pinned separately in the adapter project.
