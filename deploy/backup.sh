#!/usr/bin/env bash
# GRSMS nightly backup (Phase 13). Runs inside the "backup" container of docker-compose.prod.yml.
#
#   * Every day after BACKUP_HOUR_GMT (default 01:00 GMT):
#       grsms-db-YYYYMMDD-HHMM.sql.gz       the whole database (consistent snapshot, no locking)
#       grsms-files-YYYYMMDD-HHMM.tar.gz    uploaded documents, data-protection keys, saved emails
#   * Backups older than BACKUP_KEEP_DAYS (default 30) are deleted.
#   * Run one now:   docker compose -f docker-compose.prod.yml exec backup bash /backup.sh now
#
# The files archive contains the data-protection KEYS: keep the backup folder as private as the server itself,
# and copy it off the server (another machine or GREL's backup system) every day.
set -uo pipefail

HOUR=${BACKUP_HOUR_GMT:-1}
KEEP=${BACKUP_KEEP_DAYS:-30}

# The password goes in a private options file, so it never shows in the process list.
OPTS=$(mktemp)
chmod 600 "$OPTS"
printf '[client]\nhost=db\nuser=grsms\npassword=%s\n' "$MYSQL_PASSWORD" > "$OPTS"

backup() {
    local stamp db files
    stamp=$(date -u +%Y%m%d-%H%M)
    db="/backups/grsms-db-$stamp.sql.gz"
    files="/backups/grsms-files-$stamp.tar.gz"

    if mysqldump --defaults-extra-file="$OPTS" --single-transaction --routines --triggers --no-tablespaces grsms | gzip > "$db.part"; then
        mv "$db.part" "$db"
    else
        rm -f "$db.part"
        echo "$(date -u +%FT%TZ) BACKUP FAILED: database dump" >&2
        return 1
    fi

    if tar -czf "$files.part" -C /app-data .; then
        mv "$files.part" "$files"
    else
        rm -f "$files.part"
        echo "$(date -u +%FT%TZ) BACKUP FAILED: files archive" >&2
        return 1
    fi

    find /backups -maxdepth 1 -name 'grsms-*' -type f -mtime +"$KEEP" -delete
    echo "$(date -u +%FT%TZ) backup done: $(basename "$db") ($(du -h "$db" | cut -f1)), $(basename "$files") ($(du -h "$files" | cut -f1))"
}

if [ "${1:-}" = "now" ]; then
    backup
    exit $?
fi

echo "$(date -u +%FT%TZ) GRSMS backup service started: daily after ${HOUR}:00 GMT, keeping ${KEEP} days"
last=""
while true; do
    today=$(date -u +%Y%m%d)
    if [ "$(date -u +%-H)" -ge "$HOUR" ] && [ "$last" != "$today" ]; then
        backup && last=$today
    fi
    sleep 300
done
