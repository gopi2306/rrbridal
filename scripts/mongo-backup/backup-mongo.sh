#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
ENV_FILE="${ENV_FILE:-$REPO_ROOT/central-backend/.env}"

log() {
  echo "[$(date '+%Y-%m-%d %H:%M:%S')] $*"
}

parse_bool() {
  local value="${1,,}"
  case "$value" in
    true | 1 | yes) echo "true" ;;
    false | 0 | no | "") echo "false" ;;
    *) echo "false" ;;
  esac
}

read_env_var() {
  local key="$1"
  local default="${2:-}"
  if [[ ! -f "$ENV_FILE" ]]; then
    echo "$default"
    return
  fi
  local line
  line="$(grep -E "^[[:space:]]*${key}=" "$ENV_FILE" | tail -n 1 || true)"
  if [[ -z "$line" ]]; then
    echo "$default"
    return
  fi
  local value="${line#*=}"
  value="${value%$'\r'}"
  value="${value#"${value%%[![:space:]]*}"}"
  value="${value%"${value##*[![:space:]]}"}"
  if [[ "$value" =~ ^\".*\"$ ]]; then
    value="${value:1:${#value}-2}"
  elif [[ "$value" =~ ^\'.*\'$ ]]; then
    value="${value:1:${#value}-2}"
  fi
  echo "$value"
}

if [[ ! -f "$ENV_FILE" ]]; then
  log "ERROR: .env not found at $ENV_FILE"
  exit 1
fi

BACKUP_ENABLED="$(parse_bool "$(read_env_var BACKUP_ENABLED false)")"
if [[ "$BACKUP_ENABLED" != "true" ]]; then
  log "Backup disabled (BACKUP_ENABLED=false). Skipping."
  exit 0
fi

BACKUP_PLATFORM="$(read_env_var BACKUP_PLATFORM linux)"
BACKUP_PLATFORM="${BACKUP_PLATFORM,,}"
if [[ "$BACKUP_PLATFORM" == "windows" ]]; then
  log "WARNING: BACKUP_PLATFORM=windows but backup-mongo.sh (Linux) is running."
fi

BACKUP_PATH="$(read_env_var BACKUP_PATH "")"
if [[ -z "$BACKUP_PATH" ]]; then
  if [[ "$BACKUP_PLATFORM" == "windows" ]]; then
    BACKUP_PATH='C:\data\rr-bridal\mongo-backups'
  else
    BACKUP_PATH='/var/backups/rr-bridal/mongo'
  fi
fi

BACKUP_RETENTION_DAYS="$(read_env_var BACKUP_RETENTION_DAYS 10)"
BACKUP_INCLUDE_ENV="$(parse_bool "$(read_env_var BACKUP_INCLUDE_ENV false)")"
MONGO_URI="$(read_env_var MONGO_URI 'mongodb://localhost:27017/rr_bridal_central')"

if ! command -v mongodump >/dev/null 2>&1; then
  log "ERROR: mongodump not found. Install MongoDB Database Tools."
  exit 1
fi

DATE="$(date '+%Y-%m-%d')"
DEST="$BACKUP_PATH/$DATE"

mkdir -p "$DEST"
log "Starting mongodump to $DEST"
if ! mongodump --uri="$MONGO_URI" --out="$DEST"; then
  log "ERROR: mongodump failed"
  exit 1
fi
log "mongodump completed"

if [[ "$BACKUP_INCLUDE_ENV" == "true" ]]; then
  cp "$ENV_FILE" "$DEST/env.backup"
  log "Copied .env to $DEST/env.backup"
fi

if [[ "$BACKUP_RETENTION_DAYS" =~ ^[0-9]+$ ]] && [[ "$BACKUP_RETENTION_DAYS" -gt 0 ]]; then
  log "Pruning backups older than $BACKUP_RETENTION_DAYS days in $BACKUP_PATH"
  cutoff_epoch="$(date -d "$DATE - $BACKUP_RETENTION_DAYS days" '+%s' 2>/dev/null || date -v-"${BACKUP_RETENTION_DAYS}"d -j -f '%Y-%m-%d' "$DATE" '+%s' 2>/dev/null || true)"
  if [[ -z "$cutoff_epoch" ]]; then
    log "WARNING: Could not compute cutoff date for pruning; skipping prune."
  else
    shopt -s nullglob
    for dir in "$BACKUP_PATH"/*; do
      [[ -d "$dir" ]] || continue
      name="$(basename "$dir")"
      [[ "$name" =~ ^[0-9]{4}-[0-9]{2}-[0-9]{2}$ ]] || continue
      dir_epoch="$(date -d "$name" '+%s' 2>/dev/null || date -j -f '%Y-%m-%d' "$name" '+%s' 2>/dev/null || echo 0)"
      if [[ "$dir_epoch" -gt 0 && "$dir_epoch" -lt "$cutoff_epoch" ]]; then
        rm -rf "$dir"
        log "Deleted old backup: $dir"
      fi
    done
    shopt -u nullglob
  fi
fi

log "Backup finished successfully"
