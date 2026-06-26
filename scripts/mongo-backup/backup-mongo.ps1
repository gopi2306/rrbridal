#Requires -Version 5.1
$ErrorActionPreference = 'Stop'

function Write-Log {
    param([string]$Message)
    $timestamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
    Write-Host "[$timestamp] $Message"
}

function Parse-Bool {
    param([string]$Value)
    switch ($Value.Trim().ToLowerInvariant()) {
        { $_ -in 'true', '1', 'yes' } { return $true }
        default { return $false }
    }
}

function Read-EnvVar {
    param(
        [string]$Key,
        [string]$Default = ''
    )
    if (-not (Test-Path -LiteralPath $EnvFile)) {
        return $Default
    }
    $line = Get-Content -LiteralPath $EnvFile -Encoding UTF8 |
        Where-Object { $_ -match "^\s*$([regex]::Escape($Key))\s*=" } |
        Select-Object -Last 1
    if (-not $line) {
        return $Default
    }
    $value = ($line -split '=', 2)[1].Trim()
    if (($value.StartsWith('"') -and $value.EndsWith('"')) -or ($value.StartsWith("'") -and $value.EndsWith("'"))) {
        $value = $value.Substring(1, $value.Length - 2)
    }
    return $value
}

function Find-Mongodump {
    $cmd = Get-Command mongodump -ErrorAction SilentlyContinue
    if ($cmd) {
        return $cmd.Source
    }
    $candidates = @(
        'C:\Program Files\MongoDB\Tools\*\bin\mongodump.exe',
        'C:\Program Files\MongoDB\Database Tools\*\bin\mongodump.exe'
    )
    foreach ($pattern in $candidates) {
        $found = Get-ChildItem -Path $pattern -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($found) {
            return $found.FullName
        }
    }
    return $null
}

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = Resolve-Path (Join-Path $ScriptDir '..\..')
if ($env:ENV_FILE) {
    $EnvFile = $env:ENV_FILE
} else {
    $EnvFile = Join-Path $RepoRoot 'central-backend\.env'
}

if (-not (Test-Path -LiteralPath $EnvFile)) {
    Write-Log "ERROR: .env not found at $EnvFile"
    exit 1
}

if (-not (Parse-Bool (Read-EnvVar -Key 'BACKUP_ENABLED' -Default 'false'))) {
    Write-Log 'Backup disabled (BACKUP_ENABLED=false). Skipping.'
    exit 0
}

$backupPlatform = (Read-EnvVar -Key 'BACKUP_PLATFORM' -Default 'linux').ToLowerInvariant()
if ($backupPlatform -eq 'linux') {
    Write-Log 'WARNING: BACKUP_PLATFORM=linux but backup-mongo.ps1 (Windows) is running.'
}

$backupPath = Read-EnvVar -Key 'BACKUP_PATH' -Default ''
if ([string]::IsNullOrWhiteSpace($backupPath)) {
    if ($backupPlatform -eq 'windows') {
        $backupPath = 'C:\data\rr-bridal\mongo-backups'
    } else {
        $backupPath = '/var/backups/rr-bridal/mongo'
    }
}

$retentionDaysText = Read-EnvVar -Key 'BACKUP_RETENTION_DAYS' -Default '10'
$retentionDays = 0
[void][int]::TryParse($retentionDaysText, [ref]$retentionDays)

$includeEnv = Parse-Bool (Read-EnvVar -Key 'BACKUP_INCLUDE_ENV' -Default 'false')
$mongoUri = Read-EnvVar -Key 'MONGO_URI' -Default 'mongodb://localhost:27017/rr_bridal_central'

$mongodump = Find-Mongodump
if (-not $mongodump) {
    Write-Log 'ERROR: mongodump not found. Install MongoDB Database Tools.'
    exit 1
}

$date = Get-Date -Format 'yyyy-MM-dd'
$dest = Join-Path $backupPath $date
New-Item -ItemType Directory -Path $dest -Force | Out-Null

Write-Log "Starting mongodump to $dest"
& $mongodump --uri="$mongoUri" --out="$dest"
if ($LASTEXITCODE -ne 0) {
    Write-Log 'ERROR: mongodump failed'
    exit $LASTEXITCODE
}
Write-Log 'mongodump completed'

if ($includeEnv) {
    $envBackup = Join-Path $dest 'env.backup'
    Copy-Item -LiteralPath $EnvFile -Destination $envBackup -Force
    Write-Log "Copied .env to $envBackup"
}

if ($retentionDays -gt 0) {
    Write-Log "Pruning backups older than $retentionDays days in $backupPath"
    if (-not (Test-Path -LiteralPath $backupPath)) {
        Write-Log 'No backup root yet; skipping prune.'
    } else {
        $cutoff = (Get-Date).Date.AddDays(-$retentionDays)
        Get-ChildItem -LiteralPath $backupPath -Directory | ForEach-Object {
            if ($_.Name -notmatch '^\d{4}-\d{2}-\d{2}$') {
                return
            }
            $folderDate = [datetime]::ParseExact($_.Name, 'yyyy-MM-dd', $null)
            if ($folderDate -lt $cutoff) {
                Remove-Item -LiteralPath $_.FullName -Recurse -Force
                Write-Log "Deleted old backup: $($_.FullName)"
            }
        }
    }
}

Write-Log 'Backup finished successfully'
