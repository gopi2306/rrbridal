[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [string]$Runtime = "win-x64",

    [string]$OutputDirectory = "",

    [string]$SingleFileOutputDirectory = ""
)

$ErrorActionPreference = "Stop"

if ($env:OS -ne "Windows_NT") {
    throw "Self-contained WPF publish requires Windows with the .NET Desktop SDK."
}

$root = Split-Path -Parent $PSScriptRoot
$appProject = Join-Path $root "src\RRBridal.StoreBilling.App\RRBridal.StoreBilling.App.csproj"
$exeName = "RRBridal.StoreBilling.App.exe"

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $root "dist\billing-client-$Runtime"
}
if ([string]::IsNullOrWhiteSpace($SingleFileOutputDirectory)) {
    $SingleFileOutputDirectory = Join-Path $root "dist\billing-client-$Runtime-single"
}

$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$SingleFileOutputDirectory = [IO.Path]::GetFullPath($SingleFileOutputDirectory)

function Publish-BillingClient {
    param(
        [string]$OutDir,
        [bool]$SingleFile,
        [string]$Label
    )

    Write-Host ""
    Write-Host "Publishing store billing client ($Label)..."
    Write-Host "  Project: $appProject"
    Write-Host "  Output:  $OutDir"

    if (Test-Path $OutDir) {
        Remove-Item -Recurse -Force $OutDir
    }
    New-Item -ItemType Directory -Path $OutDir | Out-Null

    $singleFileArg = if ($SingleFile) { "true" } else { "false" }

    dotnet publish $appProject `
        --configuration $Configuration `
        --runtime $Runtime `
        --self-contained true `
        --output $OutDir `
        /p:PublishSingleFile=$singleFileArg `
        /p:IncludeNativeLibrariesForSelfExtract=true `
        /p:EnableCompressionInSingleFile=$singleFileArg

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish ($Label) failed with exit code $LASTEXITCODE"
    }

    $exePath = Join-Path $OutDir $exeName
    if (-not (Test-Path $exePath)) {
        throw "Publish ($Label) succeeded but $exeName was not found in $OutDir"
    }

    # Never ship a till .env in the base artifact; Nest generates it per store/counter download.
    $envBesideExe = Join-Path $OutDir ".env"
    if (Test-Path $envBesideExe) {
        Remove-Item -Force $envBesideExe
    }

    return $exePath
}

$folderExe = Publish-BillingClient -OutDir $OutputDirectory -SingleFile $false -Label "folder for format=zip"
$singleExe = Publish-BillingClient -OutDir $SingleFileOutputDirectory -SingleFile $true -Label "single-file for format=exe"

Write-Host ""
Write-Host "Publish complete."
Write-Host "Deploy both artifacts to the Linux AWS host:"
Write-Host "  Folder (format=zip):"
Write-Host "    rsync -av --delete `"$OutputDirectory/`" user@api-host:/var/lib/rr-bridal/billing-client-win-x64/"
Write-Host "  Single EXE (format=exe):"
Write-Host "    scp `"$singleExe`" user@api-host:/var/lib/rr-bridal/billing-client-win-x64-single/$exeName"
Write-Host ""
Write-Host "Then set on the API host:"
Write-Host "  BILLING_CLIENT_ARTIFACT_DIR=/var/lib/rr-bridal/billing-client-win-x64"
Write-Host "  BILLING_CLIENT_SINGLE_EXE_PATH=/var/lib/rr-bridal/billing-client-win-x64-single/$exeName"
Write-Host "  PUBLIC_CENTRAL_API_BASE=https://your-public-api-host"
Write-Host ""
Write-Host "Folder EXE: $folderExe"
Write-Host "Single EXE: $singleExe"
