#!/usr/bin/env bash
# Full SQL Server backup of the running stack into ./backups (mounted into the db container),
# keeping the newest $KEEP files. Schedule it, e.g. daily at 03:15:
#   15 3 * * * cd /opt/hallagi && ./deploy/backup.sh >> backups/backup.log 2>&1
set -euo pipefail
cd "$(dirname "$0")/.."

set -a; [ -f .env ] && . ./.env; set +a
DB_NAME="${DB_NAME:-Hallagi}"
KEEP="${KEEP:-14}"
FILE="${DB_NAME}_$(date -u +%Y%m%dT%H%M%SZ).bak"

mkdir -p backups
docker compose exec -T db /opt/mssql-tools18/bin/sqlcmd -C -b -S localhost -U sa -P "$DB_PASSWORD" \
  -Q "BACKUP DATABASE [$DB_NAME] TO DISK = N'/var/opt/mssql/backup/$FILE' WITH COMPRESSION, CHECKSUM, INIT"

echo "backup written: backups/$FILE"
ls -1t backups/"${DB_NAME}"_*.bak | tail -n +"$((KEEP + 1))" | xargs -r rm --
