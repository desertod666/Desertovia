param([ValidateSet('Build', 'Backend', 'Frontend', 'DomainTests')][string]$Mode = 'Backend')

$ErrorActionPreference = 'Stop'
$gameRoot = Split-Path $PSScriptRoot -Parent
$localSdk = [IO.Path]::GetFullPath((Join-Path $gameRoot '../tools/dotnet'))
if (Test-Path (Join-Path $localSdk 'dotnet.exe')) {
    $env:DOTNET_ROOT = $localSdk
    $env:Path = $localSdk + [IO.Path]::PathSeparator + $env:Path
}
$bundledNode = Join-Path $env:USERPROFILE '.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin'
if (Test-Path (Join-Path $bundledNode 'node.exe')) {
    $env:Path = $bundledNode + [IO.Path]::PathSeparator + $env:Path
}
$env:DOTNET_NOLOGO = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'

Push-Location $gameRoot
try {
    switch ($Mode) {
        'Build' { & dotnet build src/Desertod.Api -c Debug -v:q }
        'DomainTests' { & dotnet test tests/Desertod.Domain.Tests -c Debug --logger trx --results-directory TestResults/local }
        'Backend' {
            $env:ASPNETCORE_ENVIRONMENT = 'Development'
            $env:ASPNETCORE_URLS = 'http://127.0.0.1:5180'
            $env:ConnectionStrings__Desertod = 'Host=127.0.0.1;Port=55432;Database=desertovia_foundation;Username=desertovia_dev;Password=desertovia_dev_only'
            $env:Game__AlmightyEnabled = 'false'
            $env:Game__HospitalEnabled = 'false'
            $env:Authentication__Telegram__BotToken = ''
            $env:Authentication__Telegram__BotUsername = ''
            $env:Authentication__Telegram__BrowserBridgeUrl = ''
            & dotnet run --project src/Desertod.Api --no-launch-profile
        }
        'Frontend' {
            if ([int]((& node --version).TrimStart('v').Split('.')[0]) -lt 24) {
                throw 'Node.js 24 or newer is required.'
            }
            $env:DESERTOD_BACKEND = 'http://127.0.0.1:5180'
            Set-Location (Join-Path $gameRoot 'web')
            & npm run dev -- --host 127.0.0.1 --port 5190 --strictPort
        }
    }
    if ($LASTEXITCODE -ne 0) { throw "$Mode failed with exit code $LASTEXITCODE" }
} finally { Pop-Location }
