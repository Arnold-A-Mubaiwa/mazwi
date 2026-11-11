param([Parameter(Mandatory = $true)][string]$ArtistName, [switch]$Apply)

# One-off: copies your unowned stories by one artist name from the old shared test database
# (MAZWI_TEST_CONNECTION_STRING) into the app database (ConnectionStrings__Mazwi). Without -Apply it only lists them.
$ErrorActionPreference = 'Stop'
$apiDll = Join-Path $PSScriptRoot '../src/Mazwi.WebApi/bin/Debug/net10.0/Mazwi.WebApi.dll'
. (Join-Path $PSScriptRoot 'Set-LocalEnvironment.ps1')
if ([string]::IsNullOrWhiteSpace($env:ConnectionStrings__Mazwi) -or [string]::IsNullOrWhiteSpace($env:MAZWI_TEST_CONNECTION_STRING)) {
    throw 'Set both ConnectionStrings__Mazwi (app) and MAZWI_TEST_CONNECTION_STRING (old shared database) in the ignored .env.'
}
if (-not (Test-Path -LiteralPath $apiDll)) { throw 'Build Mazwi first: run ./scripts/Start-Mazwi.ps1 once.' }
$env:ConnectionStrings__MazwiImportSource = $env:MAZWI_TEST_CONNECTION_STRING
$dotnet = (Get-Command dotnet -ErrorAction Stop).Source
& $dotnet $apiDll --migrate
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$arguments = @($apiDll, '--import-stories', $ArtistName)
if ($Apply) { $arguments += '--apply' }
& $dotnet @arguments
exit $LASTEXITCODE
