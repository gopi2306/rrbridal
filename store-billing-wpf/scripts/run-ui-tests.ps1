[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [ValidateSet("Both", "Online", "Offline")]
    [string]$Mode = "Both",
    [string]$Category = "",
    [string]$MongoUri = "mongodb://127.0.0.1:27017",
    [ValidateRange(1, 20)]
    [int]$Repeat = 1,
    [string]$ArtifactsDirectory = "",
    [switch]$IncludeExistingTests
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$appProject = Join-Path $root "src\RRBridal.StoreBilling.App\RRBridal.StoreBilling.App.csproj"
$existingTestProject = Join-Path $root "src\RRBridal.StoreBilling.Tests\RRBridal.StoreBilling.Tests.csproj"
$uiTestProject = Join-Path $root "src\RRBridal.StoreBilling.UiTests\RRBridal.StoreBilling.UiTests.csproj"
$appExe = Join-Path $root "src\RRBridal.StoreBilling.App\bin\$Configuration\net9.0-windows\RRBridal.StoreBilling.App.exe"

if ($env:OS -ne "Windows_NT") {
    throw "WPF UI automation requires Windows."
}

if (-not [Environment]::UserInteractive) {
    throw "WPF UI automation requires an interactive desktop session."
}

if ($Mode -ne "Online") {
    $mongoEndpoint = [Uri]$MongoUri
    $mongoPort = if ($mongoEndpoint.Port -gt 0) { $mongoEndpoint.Port } else { 27017 }
    $mongoReady = Test-NetConnection $mongoEndpoint.Host -Port $mongoPort -InformationLevel Quiet
    if (-not $mongoReady) {
        throw "Offline UI automation requires MongoDB at $($mongoEndpoint.Host):$mongoPort."
    }
}

if ([string]::IsNullOrWhiteSpace($ArtifactsDirectory)) {
    $ArtifactsDirectory = Join-Path $root "TestResults\ui"
}

$env:RRBRIDAL_UI_APP_EXE = $appExe
$env:RRBRIDAL_UI_ARTIFACTS = [IO.Path]::GetFullPath($ArtifactsDirectory)
$env:RRBRIDAL_UI_TESTS_ENABLED = "1"
$env:RRBRIDAL_UI_MODE = $Mode
$env:RRBRIDAL_UI_MONGO_URI = $MongoUri

dotnet build $appProject --configuration $Configuration
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

if ($IncludeExistingTests) {
    dotnet test $existingTestProject --configuration $Configuration
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

$filters = @()
if ($Mode -eq "Online") { $filters += "Category=UiOnline" }
if ($Mode -eq "Offline") { $filters += "Category=UiOffline" }
if (-not [string]::IsNullOrWhiteSpace($Category)) { $filters += "Category=$Category" }

$runStamp = Get-Date -Format "yyyyMMdd-HHmmss"
for ($run = 1; $run -le $Repeat; $run++) {
    $testArgs = @(
        "test", $uiTestProject,
        "--configuration", $Configuration,
        "--no-restore",
        "--logger", "trx;LogFileName=ui-tests-$runStamp-$run.trx",
        "--results-directory", $env:RRBRIDAL_UI_ARTIFACTS
    )
    if ($filters.Count -gt 0) {
        $testArgs += @("--filter", ($filters -join "&"))
    }

    & dotnet @testArgs
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

exit 0
