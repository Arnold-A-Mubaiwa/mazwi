param([switch]$SkipBuild)

$ErrorActionPreference = 'Stop'
$workspaceRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$runRoot = Join-Path $workspaceRoot '.local/run'
$statePath = Join-Path $runRoot 'processes.json'
$apiRoot = Join-Path $workspaceRoot 'src/Mazwi.WebApi'
$webRoot = Join-Path $workspaceRoot 'web'
$apiDll = Join-Path $apiRoot 'bin/Debug/net10.0/Mazwi.WebApi.dll'
$nextCli = Join-Path $webRoot 'node_modules/next/dist/bin/next'

function Test-MazwiReady {
    try {
        $studio = Invoke-WebRequest -UseBasicParsing -Uri 'http://127.0.0.1:3000/api/studio?limit=1' -TimeoutSec 2
        $site = Invoke-WebRequest -UseBasicParsing -Uri 'http://127.0.0.1:3000/studio' -TimeoutSec 2
        return $studio.StatusCode -eq 200 -and $site.StatusCode -eq 200
    } catch { return $false }
}

function Test-MazwiProcess($Entry) {
    $localProcess = Get-Process -Id ([int]$Entry.id) -ErrorAction SilentlyContinue
    if (-not $localProcess) { return $false }
    return $localProcess.StartTime.ToUniversalTime().Ticks.ToString() -eq [string]$Entry.startedUtcTicks -and
        $localProcess.Path.Equals([string]$Entry.executable, [StringComparison]::OrdinalIgnoreCase)
}

$listeners = @(Get-NetTCPConnection -State Listen -ErrorAction SilentlyContinue | Where-Object { $_.LocalPort -in @(3000, 5046) })
if ($listeners.Count) {
    if (Test-Path -LiteralPath $statePath) {
        $savedState = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
        $allOwned = $savedState.workspace -eq $workspaceRoot -and @($savedState.processes).Count -eq 2
        foreach ($entry in $savedState.processes) { if (-not (Test-MazwiProcess $entry)) { $allOwned = $false } }
        $ownedIds = @($savedState.processes | ForEach-Object { [int]$_.id })
        foreach ($listener in $listeners) { if ($listener.OwningProcess -notin $ownedIds) { $allOwned = $false } }
        if ($allOwned -and (Test-MazwiReady)) {
            Write-Output 'Mazwi is already running: http://127.0.0.1:3000'
            Write-Output 'Stop Mazwi before rebuilding changed code.'
            return
        }
    }
    throw 'Ports 3000/5046 are in use. Stop your existing Mazwi terminals first. No existing process was stopped.'
}

. (Join-Path $PSScriptRoot 'Set-LocalEnvironment.ps1')
if ([string]::IsNullOrWhiteSpace($env:ConnectionStrings__Mazwi)) {
    throw 'Configure ConnectionStrings__Mazwi privately in the ignored root .env. No OpenAI key is needed for this manual demo.'
}
$env:NEXT_TELEMETRY_DISABLED = '1'
$dotnetExecutable = (Get-Command dotnet -ErrorAction Stop).Source
$nodeExecutable = (Get-Command node -ErrorAction Stop).Source
if (-not (Test-Path -LiteralPath $nextCli)) { throw 'Install the frontend dependencies using docs/frontend-setup.md first.' }
New-Item -ItemType Directory -Path $runRoot -Force | Out-Null

function Invoke-MazwiStep([string]$Executable, [string[]]$CommandArguments, [string]$Directory, [string]$Label) {
    $stepProcess = Start-Process -FilePath $Executable -ArgumentList $CommandArguments -WorkingDirectory $Directory -WindowStyle Hidden -Wait -PassThru `
        -RedirectStandardOutput (Join-Path $runRoot "$Label.out.log") -RedirectStandardError (Join-Path $runRoot "$Label.err.log")
    if ($stepProcess.ExitCode -ne 0) { throw "$Label failed. Details remain in ignored .local/run logs; credentials are not printed here." }
}

if (-not $SkipBuild) {
    Write-Output 'Building the API and website. Logs stay in .local/run.'
    Invoke-MazwiStep $dotnetExecutable @('build') $workspaceRoot 'build-api'
    Invoke-MazwiStep $nodeExecutable @(('"' + $nextCli + '"'), 'build') $webRoot 'build-web'
}
if (-not (Test-Path -LiteralPath $apiDll) -or -not (Test-Path -LiteralPath (Join-Path $webRoot '.next/BUILD_ID'))) {
    throw 'A production build is missing. Run this command without -SkipBuild.'
}
Invoke-MazwiStep $dotnetExecutable @(('"' + $apiDll + '"'), '--migrate') $apiRoot 'migrate'

$started = @()
try {
    $apiProcess = Start-Process -FilePath $dotnetExecutable -ArgumentList @(('"' + $apiDll + '"'), '--urls', 'http://127.0.0.1:5046') -WorkingDirectory $apiRoot -WindowStyle Hidden -PassThru `
        -RedirectStandardOutput (Join-Path $runRoot 'api.out.log') -RedirectStandardError (Join-Path $runRoot 'api.err.log')
    $started += $apiProcess
    $webProcess = Start-Process -FilePath $nodeExecutable -ArgumentList @(('"' + $nextCli + '"'), 'start', '--hostname', '127.0.0.1', '--port', '3000') -WorkingDirectory $webRoot -WindowStyle Hidden -PassThru `
        -RedirectStandardOutput (Join-Path $runRoot 'web.out.log') -RedirectStandardError (Join-Path $runRoot 'web.err.log')
    $started += $webProcess
    $deadline = [DateTime]::UtcNow.AddSeconds(60)
    while ([DateTime]::UtcNow -lt $deadline -and -not (Test-MazwiReady)) {
        if ($apiProcess.HasExited -or $webProcess.HasExited) { throw 'A Mazwi server exited. Check the ignored .local/run logs.' }
        Start-Sleep -Milliseconds 500
    }
    if (-not (Test-MazwiReady)) { throw 'Mazwi did not become database-ready. Check the ignored .local/run logs.' }
    $processEntries = @(
        @{ kind = 'api'; id = $apiProcess.Id; startedUtcTicks = $apiProcess.StartTime.ToUniversalTime().Ticks.ToString(); executable = $dotnetExecutable },
        @{ kind = 'web'; id = $webProcess.Id; startedUtcTicks = $webProcess.StartTime.ToUniversalTime().Ticks.ToString(); executable = $nodeExecutable }
    )
    @{ workspace = $workspaceRoot; processes = $processEntries } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $statePath -Encoding UTF8
    Write-Output 'Mazwi is ready: http://127.0.0.1:3000'
    Write-Output 'Studio: http://127.0.0.1:3000/studio'
    Write-Output 'Stop with: .\scripts\Stop-Mazwi.ps1'
} catch {
    foreach ($localProcess in $started) { if (-not $localProcess.HasExited) { Stop-Process -Id $localProcess.Id -ErrorAction SilentlyContinue } }
    throw
}
