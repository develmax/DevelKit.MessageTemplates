$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    dotnet build DevelKit.MessageTemplates.slnx -c Release --nologo
    if ($LASTEXITCODE) { throw 'Build failed' }
    dotnet run --project tests/Regression -c Release --no-build
    if ($LASTEXITCODE) { throw 'Core/SQL regression failed' }
    & ./tests/DynamicsCrm.Regression/bin/Release/net452/DynamicsCrm.Regression.exe
    if ($LASTEXITCODE) { throw 'CRM regression failed' }
    dotnet run --project samples/Meetings -c Release --no-build
    if ($LASTEXITCODE) { throw 'SQL sample failed' }
    & ./samples/CrmMeetings/bin/Release/net452/CrmMeetings.exe
    if ($LASTEXITCODE) { throw 'CRM sample failed' }
    foreach ($package in @('DevelKit.MessageTemplates', 'DevelKit.MessageTemplates.SqlServer', 'DevelKit.MessageTemplates.DynamicsCrm')) {
        dotnet pack "src/$package" -c Release --no-build -o artifacts
        if ($LASTEXITCODE) { throw "Pack failed: $package" }
    }
}
finally { Pop-Location }