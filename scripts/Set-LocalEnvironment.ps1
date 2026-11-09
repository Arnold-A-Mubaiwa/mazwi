# Dot-source from the repository root before dotnet commands. Never echoes secrets.
$env:DOTNET_CLI_HOME = Join-Path $PSScriptRoot '../.local/dotnet'
$env:NUGET_PACKAGES = Join-Path $PSScriptRoot '../.local/nuget/packages'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$localEnvFile = Join-Path $PSScriptRoot '../.env'
if (Test-Path -LiteralPath $localEnvFile) {
    foreach ($localEnvLine in [System.IO.File]::ReadAllLines($localEnvFile)) {
        if ([string]::IsNullOrWhiteSpace($localEnvLine) -or $localEnvLine.TrimStart().StartsWith('#')) { continue }
        if ($localEnvLine -notmatch '^([A-Za-z_][A-Za-z0-9_]*)=(.*)$') { throw 'Invalid .env entry; use NAME=value.' }
        $localEnvName = $Matches[1]
        $localEnvValue = $Matches[2].Trim()
        if ($localEnvValue.Length -ge 2 -and (($localEnvValue.StartsWith('"') -and $localEnvValue.EndsWith('"')) -or ($localEnvValue.StartsWith("'") -and $localEnvValue.EndsWith("'")))) {
            $localEnvValue = $localEnvValue.Substring(1, $localEnvValue.Length - 2)
        }
        [Environment]::SetEnvironmentVariable($localEnvName, $localEnvValue, 'Process')
    }
}
