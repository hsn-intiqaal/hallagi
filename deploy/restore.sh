#!/usr/bin/env bash
# Restores a backup produced by backup.sh (stops the API while restoring):
#   ./deploy/restore.sh backups/Hallagi_20260101T031500Z.bak
set -euo pipefail
cd "$(dirname "$0")/.."
[ $# -eq 1 ] && [ -f "$1" ] || { echo "usage: $0 backups/<file>.bak" >&2; exit 1; }

set -a; [ -f .env ] && . ./.env; set +a
DB_NAME="${DB_NAME:-Hallagi}"
FILE="$(basename "$1")"

read -r -p "Replace database [$DB_NAME] with $FILE? (yes/no) " answer
[ "$answer" = "yes" ] || exit 1

docker compose stop api
docker compose exec -T db /opt/mssql-tools18/bin/sqlcmd -C -b -S localhost -U sa -P "$DB_PASSWORD" -Q "
  IF DB_ID(N'$DB_NAME') IS NOT NULL ALTER DATABASE [$DB_NAME] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
  RESTORE DATABASE [$DB_NAME] FROM DISK = N'/var/opt/mssql/backup/$FILE' WITH REPLACE, CHECKSUM;
  ALTER DATABASE [$DB_NAME] SET MULTI_USER;"
docker compose start api
echo "restored $FILE"
