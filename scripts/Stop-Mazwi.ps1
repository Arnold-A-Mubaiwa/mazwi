$ErrorActionPreference = 'Stop'
$workspaceRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$statePath = Join-Path $workspaceRoot '.local/run/processes.json'
if (-not (Test-Path -LiteralPath $statePath)) { Write-Output 'No script-managed Mazwi servers are recorded. Existing terminals were left alone.'; return }
$savedState = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
if ($savedState.workspace -ne $workspaceRoot -or @($savedState.processes).Count -ne 2) { throw 'The saved process state does not match this workspace.' }
$owned = @()
foreach ($entry in $savedState.processes) {
    if ($entry.kind -notin @('api', 'web')) { throw 'Unexpected process kind; nothing was stopped.' }
    $localProcess = Get-Process -Id ([int]$entry.id) -ErrorAction SilentlyContinue
    if (-not $localProcess) { continue }
    $commandName = if ($entry.kind -eq 'api') { 'dotnet' } else { 'node' }
    $expectedExecutable = (Get-Command $commandName -ErrorAction Stop).Source
    if ($localProcess.StartTime.ToUniversalTime().Ticks.ToString() -ne [string]$entry.startedUtcTicks -or
        -not $localProcess.Path.Equals($expectedExecutable, [StringComparison]::OrdinalIgnoreCase) -or
        -not $localProcess.Path.Equals([string]$entry.executable, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'A process identity changed. Nothing was stopped; check your existing terminals.'
    }
    $owned += $localProcess
}
foreach ($localProcess in $owned) { Stop-Process -Id $localProcess.Id -ErrorAction Stop }
Remove-Item -LiteralPath $statePath
Write-Output 'Stopped only the Mazwi servers started by Start-Mazwi.ps1. Recordings and database content were kept.'
