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
$exeName = "trubill.exe"

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $root "dist\trubill-$Runtime"
}
if ([string]::IsNullOrWhiteSpace($SingleFileOutputDirectory)) {
    $SingleFileOutputDirectory = Join-Path $root "dist\trubill-$Runtime-single"
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
        /p:EnableCompressionInSingleFile=$singleFileArg `
        | Out-Host

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

    return ,$exePath
}

$folderExe = Publish-BillingClient -OutDir $OutputDirectory -SingleFile $false -Label "folder for format=zip"
$singleExe = Publish-BillingClient -OutDir $SingleFileOutputDirectory -SingleFile $true -Label "single-file for format=exe"

$setupExe = Join-Path $SingleFileOutputDirectory "setup.exe"
$iss = Join-Path $root "installer\trubill.iss"
$iscc = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1

if (-not $iscc) {
    Write-Host ""
    Write-Host "Inno Setup 6 (ISCC.exe) was not found. setup.exe was not built."
    Write-Host "Install https://jrsoftware.org/isdl.php and re-run this script to compile the installer."
} else {
    Write-Host ""
    Write-Host "Compiling TruBill setup.exe..."
    & $iscc "/DSourceDir=$SingleFileOutputDirectory" "/DOutputDir=$SingleFileOutputDirectory" $iss
    if ($LASTEXITCODE -ne 0) {
        throw "ISCC failed with exit code $LASTEXITCODE"
    }
    if (-not (Test-Path $setupExe)) {
        throw "ISCC succeeded but setup.exe was not found in $SingleFileOutputDirectory"
    }
}

Write-Host ""
Write-Host "Publish complete."
Write-Host "Deploy artifacts to the Linux AWS host:"
Write-Host "  Folder (format=zip):"
Write-Host "    rsync -av --delete `"$OutputDirectory/`" user@api-host:/var/lib/rr-bridal/trubill-win-x64/"
Write-Host "  Single EXE (format=exe):"
Write-Host "    scp `"$singleExe`" user@api-host:/var/lib/rr-bridal/trubill-win-x64-single/$exeName"
if (Test-Path $setupExe) {
    Write-Host "  Installer (format=setup):"
    Write-Host "    scp `"$setupExe`" user@api-host:/var/lib/rr-bridal/trubill-win-x64-single/setup.exe"
}
Write-Host ""
Write-Host "Then set on the API host:"
Write-Host "  BILLING_CLIENT_ARTIFACT_DIR=/var/lib/rr-bridal/trubill-win-x64"
Write-Host "  BILLING_CLIENT_SINGLE_EXE_PATH=/var/lib/rr-bridal/trubill-win-x64-single/$exeName"
Write-Host "  BILLING_CLIENT_SETUP_EXE_PATH=/var/lib/rr-bridal/trubill-win-x64-single/setup.exe"
Write-Host "  PUBLIC_CENTRAL_API_BASE=https://your-public-api-host"
Write-Host ""
Write-Host "Folder EXE: $folderExe"
Write-Host "Single EXE: $singleExe"
if (Test-Path $setupExe) {
    Write-Host "Setup EXE:  $setupExe"
}
