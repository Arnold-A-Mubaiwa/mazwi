param([Parameter(Mandatory = $true)][string]$Email)

# Gives every story saved before artist accounts existed to the account with this email.
# Run once after creating your account. Stories that already have an owner are never changed.
$ErrorActionPreference = 'Stop'
$apiDll = Join-Path $PSScriptRoot '../src/Mazwi.WebApi/bin/Debug/net10.0/Mazwi.WebApi.dll'
. (Join-Path $PSScriptRoot 'Set-LocalEnvironment.ps1')
if ([string]::IsNullOrWhiteSpace($env:ConnectionStrings__Mazwi)) {
    throw 'Configure ConnectionStrings__Mazwi privately in the ignored root .env first.'
}
if (-not (Test-Path -LiteralPath $apiDll)) { throw 'Build Mazwi first: run ./scripts/Start-Mazwi.ps1 once.' }
& (Get-Command dotnet -ErrorAction Stop).Source $apiDll --claim-stories $Email
exit $LASTEXITCODE
