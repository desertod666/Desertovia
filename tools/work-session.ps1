param(
    [ValidateSet('Start','Renew','Stop','Worker')][string]$Mode = 'Renew',
    [string]$StateDirectory = (Join-Path (Split-Path $PSScriptRoot -Parent) '../qa/work-session'),
    [ValidateRange(2,60)][int]$LeaseMinutes = 30
)

$ErrorActionPreference = 'Stop'
$stateRoot = [IO.Path]::GetFullPath($StateDirectory)
$scriptFile = [IO.Path]::GetFullPath($PSCommandPath)
[IO.Directory]::CreateDirectory($stateRoot) | Out-Null
$leaseFile = Join-Path $stateRoot 'awake-lease.txt'
$statusFile = Join-Path $stateRoot 'awake-status.json'

function Read-Worker {
    if (!(Test-Path -LiteralPath $statusFile)) { return $null }
    try {
        $saved = Get-Content -LiteralPath $statusFile -Raw | ConvertFrom-Json
        if (!$saved.active) { return $null }
        $process = Get-CimInstance Win32_Process -Filter ('ProcessId=' + [int]$saved.processId)
        if ($process -and $process.Name -eq 'pwsh.exe' -and $process.CommandLine.Contains($scriptFile) -and $process.CommandLine.Contains('-Mode Worker')) {
            return $saved
        }
    } catch { return $null }
    return $null
}

if ($Mode -eq 'Worker') {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class WorkSessionPower {
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern uint SetThreadExecutionState(uint flags);
}
'@
    $accepted = [WorkSessionPower]::SetThreadExecutionState(2147483649) # CONTINUOUS | SYSTEM_REQUIRED; display may turn off.
    if ($accepted -eq 0) { throw 'Windows refused the system-awake request.' }
    $status = @{ processId=$PID; active=$true; script=$scriptFile; startedAtUtc=[DateTime]::UtcNow.ToString('o'); systemAwake=$true; displayRequired=$false; nativeReturn=$accepted; leaseMinutes=$LeaseMinutes }
    $status | ConvertTo-Json | Set-Content -LiteralPath $statusFile -Encoding utf8
    try {
        while (Test-Path -LiteralPath $leaseFile) {
            $expires = [DateTime]::Parse((Get-Content -LiteralPath $leaseFile -Raw).Trim()).ToUniversalTime()
            if ([DateTime]::UtcNow -ge $expires) { break }
            Start-Sleep -Seconds 10
        }
    } finally {
        [WorkSessionPower]::SetThreadExecutionState(2147483648) | Out-Null
        $status.active=$false; $status.systemAwake=$false; $status.stoppedAtUtc=[DateTime]::UtcNow.ToString('o')
        $status | ConvertTo-Json | Set-Content -LiteralPath $statusFile -Encoding utf8
    }
    exit
}

if ($Mode -eq 'Stop') {
    if (Test-Path -LiteralPath $leaseFile) { [IO.File]::Delete($leaseFile) }
    Write-Output 'Work-session sleep protection lease withdrawn; the owned worker releases its request within10seconds.'
    exit
}

$expiresAt = [DateTime]::UtcNow.AddMinutes($LeaseMinutes).ToString('o')
[IO.File]::WriteAllText($leaseFile, $expiresAt)
$worker = Read-Worker
if (!$worker) {
    $pwsh = (Get-Process -Id $PID).Path
    $arguments = '-NoProfile -File "' + $scriptFile + '" -Mode Worker -StateDirectory "' + $stateRoot + '" -LeaseMinutes ' + $LeaseMinutes
    $started = Start-Process -FilePath $pwsh -ArgumentList $arguments -WindowStyle Hidden -PassThru
    Write-Output ('Started owned system-awake worker: ' + $started.Id)
}
Write-Output ('Sleep protection expires without renewal at UTC ' + $expiresAt)
