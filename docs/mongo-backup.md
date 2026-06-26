# MongoDB daily backup

Daily `mongodump` for the central database (`rr_bridal_central`), with dated folders and automatic pruning. All settings live in [`central-backend/.env`](../central-backend/.env.example) — no separate config file.

Scripts:

| OS | Script |
|----|--------|
| Linux | [`scripts/mongo-backup/backup-mongo.sh`](../scripts/mongo-backup/backup-mongo.sh) |
| Windows | [`scripts/mongo-backup/backup-mongo.ps1`](../scripts/mongo-backup/backup-mongo.ps1) |

## Prerequisites

Install [MongoDB Database Tools](https://www.mongodb.com/docs/database-tools/) so `mongodump` is on `PATH` (Linux) or discoverable (Windows).

## `.env` settings

Add these to `central-backend/.env` (see [`.env.example`](../central-backend/.env.example)):

| Variable | Type | Purpose | Default |
|----------|------|---------|---------|
| `BACKUP_ENABLED` | bool | Master switch — `false` skips backup (clean exit) | `false` |
| `BACKUP_PLATFORM` | `linux` \| `windows` | Must match the script you schedule | `linux` |
| `BACKUP_PATH` | string | Root folder for dated dump directories | `/var/backups/rr-bridal/mongo` (linux) or `C:\data\rr-bridal\mongo-backups` (windows) if empty |
| `BACKUP_RETENTION_DAYS` | number | Delete dated folders older than this | `10` |
| `BACKUP_INCLUDE_ENV` | bool | Copy `.env` into each day's folder as `env.backup` | `false` |

`MONGO_URI` in the same file is used for the dump.

Bool values accept `true`/`false`, `1`/`0`, or `yes`/`no` (case-insensitive).

## Backup layout

```
{BACKUP_PATH}/
  2026-06-24/
    rr_bridal_central/     # mongodump output
    env.backup             # only when BACKUP_INCLUDE_ENV=true
```

## Manual test

**Linux** (from repo root):

```bash
chmod +x scripts/mongo-backup/backup-mongo.sh
./scripts/mongo-backup/backup-mongo.sh
```

**Windows** (from repo root, PowerShell):

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\mongo-backup\backup-mongo.ps1
```

Set `BACKUP_ENABLED=true` in `central-backend/.env` before testing.

## Schedule daily runs

### Linux (cron)

Example — daily at 2:00 AM:

```bash
0 2 * * * cd /path/to/RR_Bridal && ./scripts/mongo-backup/backup-mongo.sh >> /var/log/rr-bridal-mongo-backup.log 2>&1
```

### Windows (Task Scheduler)

1. Create a task with a daily trigger (e.g. 2:00 AM).
2. Action: **Start a program**
   - Program: `powershell.exe`
   - Arguments: `-ExecutionPolicy Bypass -File "D:\path\to\RR_Bridal\scripts\mongo-backup\backup-mongo.ps1"`
   - Start in: repo root (`D:\path\to\RR_Bridal`)

The script reads `.env` on every run. Set `BACKUP_ENABLED=false` to disable backups without removing the scheduled task.

## Restore

**Database** (replace date and URI as needed):

```bash
mongorestore --uri="mongodb://localhost:27017/rr_bridal_central" --drop /var/backups/rr-bridal/mongo/2026-06-24/rr_bridal_central
```

**`.env`** (only if `BACKUP_INCLUDE_ENV=true` was used when the backup was taken):

```bash
cp /var/backups/rr-bridal/mongo/2026-06-24/env.backup central-backend/.env
```
