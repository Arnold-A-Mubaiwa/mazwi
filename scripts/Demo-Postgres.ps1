# Portable PostgreSQL for the judge demo (dot-sourced by Start-Mazwi.ps1 -Demo and Stop-Mazwi.ps1).
# It downloads the zonky embedded-postgres Windows binaries (PostgreSQL licence) from Maven Central once, checks their
# SHA-256 before running anything, and runs a private server for the ignored .local/demo-postgres folder only:
# 127.0.0.1, port 5439, no password (local demo data only, never for real artists).
$script:DemoPgVersion = '18.6.0'
$script:DemoPgSha256 = 'B7EF2D03588E0439F0D0A2250F43905F7464F1E96ACE47703CEAF79094198150'
$script:DemoPgPort = 5439

function Get-MazwiDemoPaths([string]$WorkspaceRoot) {
    $root = Join-Path $WorkspaceRoot '.local/demo-postgres'
    return @{ Root = $root; Bin = (Join-Path $root 'pg/bin'); Data = (Join-Path $root 'data'); Logs = (Join-Path $root 'logs') }
}

function Invoke-MazwiDemoTool([string]$Executable, [string[]]$Arguments, [string]$LogFolder, [string]$Label) {
    # Wait for the tool only (not for a server it starts), and keep its output in log files.
    $tool = Start-Process -FilePath $Executable -ArgumentList $Arguments -WindowStyle Hidden -PassThru `
        -RedirectStandardOutput (Join-Path $LogFolder "$Label.out.log") -RedirectStandardError (Join-Path $LogFolder "$Label.err.log")
    if (-not $tool.WaitForExit(120000)) { throw "$Label did not finish. See .local/demo-postgres/logs." }
    if ($tool.ExitCode -ne 0) { throw "$Label failed. See .local/demo-postgres/logs." }
}

function Test-MazwiDemoPort { return [bool](Get-NetTCPConnection -State Listen -LocalPort $script:DemoPgPort -ErrorAction SilentlyContinue) }

function Start-MazwiDemoDatabase([string]$WorkspaceRoot) {
    foreach ($runtime in @('vcruntime140.dll', 'vcruntime140_1.dll', 'msvcp140.dll')) {
        if (-not (Test-Path -LiteralPath (Join-Path $env:SystemRoot "System32/$runtime"))) {
            throw "The demo database needs the Microsoft Visual C++ Redistributable (x64). Install it from https://aka.ms/vs/17/release/vc_redist.x64.exe and run this again."
        }
    }
    $paths = Get-MazwiDemoPaths $WorkspaceRoot
    New-Item -ItemType Directory -Path $paths.Root, $paths.Logs -Force | Out-Null
    if (-not (Test-Path -LiteralPath (Join-Path $paths.Bin 'postgres.exe'))) {
        $download = Join-Path $paths.Root 'download'
        New-Item -ItemType Directory -Path $download -Force | Out-Null
        $jar = Join-Path $download 'postgres.zip'
        $url = "https://repo1.maven.org/maven2/io/zonky/test/postgres/embedded-postgres-binaries-windows-amd64/$($script:DemoPgVersion)/embedded-postgres-binaries-windows-amd64-$($script:DemoPgVersion).jar"
        Write-Output "Downloading a portable PostgreSQL $($script:DemoPgVersion) for the demo (about 25 MB, once)."
        $ProgressPreference = 'SilentlyContinue'
        Invoke-WebRequest -UseBasicParsing -Uri $url -OutFile $jar
        if ((Get-FileHash -LiteralPath $jar -Algorithm SHA256).Hash -ne $script:DemoPgSha256) {
            Remove-Item -LiteralPath $jar -Force
            throw 'The downloaded PostgreSQL did not match its expected SHA-256 and was deleted. Nothing was run.'
        }
        $unpacked = Join-Path $download 'jar'
        Expand-Archive -LiteralPath $jar -DestinationPath $unpacked -Force
        New-Item -ItemType Directory -Path (Join-Path $paths.Root 'pg') -Force | Out-Null
        & tar.exe -xf (Join-Path $unpacked 'postgres-windows-x86_64.txz') -C (Join-Path $paths.Root 'pg')
        if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath (Join-Path $paths.Bin 'postgres.exe'))) { throw 'Unpacking the demo PostgreSQL failed.' }
    }
    if (-not (Test-Path -LiteralPath (Join-Path $paths.Data 'PG_VERSION'))) {
        Invoke-MazwiDemoTool (Join-Path $paths.Bin 'initdb.exe') @('-D', ('"' + $paths.Data + '"'), '-U', 'postgres', '-A', 'trust', '-E', 'UTF8', '--locale=C') $paths.Logs 'initdb'
    }
    $pidFile = Join-Path $paths.Data 'postmaster.pid'
    if (Test-MazwiDemoPort) {
        if (-not (Test-Path -LiteralPath $pidFile)) { throw "Port $($script:DemoPgPort) is used by another program. Stop it, then run this again. Nothing was stopped." }
    } else {
        Invoke-MazwiDemoTool (Join-Path $paths.Bin 'pg_ctl.exe') @('-D', ('"' + $paths.Data + '"'), '-o', ('"-p ' + $script:DemoPgPort + ' -h 127.0.0.1"'),
            '-l', ('"' + (Join-Path $paths.Logs 'postgres.log') + '"'), '-w', 'start') $paths.Logs 'pg_ctl-start'
        if (-not (Test-MazwiDemoPort)) { throw 'The demo PostgreSQL did not start. See .local/demo-postgres/logs.' }
    }
    return "Host=127.0.0.1;Port=$($script:DemoPgPort);Database=mazwi_demo;Username=postgres"
}

function Stop-MazwiDemoDatabase([string]$WorkspaceRoot) {
    $paths = Get-MazwiDemoPaths $WorkspaceRoot
    if (-not (Test-Path -LiteralPath (Join-Path $paths.Data 'postmaster.pid'))) { return $false }
    Invoke-MazwiDemoTool (Join-Path $paths.Bin 'pg_ctl.exe') @('-D', ('"' + $paths.Data + '"'), '-m', 'fast', '-w', 'stop') $paths.Logs 'pg_ctl-stop'
    return $true
}
