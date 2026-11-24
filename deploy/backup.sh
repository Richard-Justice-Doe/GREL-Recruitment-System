#!/usr/bin/env bash
# GRSMS nightly backup (Phase 13; encryption 22 Nov 2026, docs/41 H4). Runs inside the "backup" container of
# docker-compose.prod.yml (image: deploy/backup.Dockerfile = MySQL client + GnuPG).
#
#   * Every day after BACKUP_HOUR_GMT (default 01:00 GMT):
#       grsms-db-YYYYMMDD-HHMM.sql.gz[.gpg]      the whole database (consistent snapshot, no locking)
#       grsms-files-YYYYMMDD-HHMM.tar.gz[.gpg]   uploaded documents and saved emails (WITHOUT the keys)
#       grsms-keys-YYYYMMDD-HHMM.tar.gz[.gpg]    the data-protection keys (sign-in cookies, encrypted settings), on their own
#   * ENCRYPTED when a public key (.asc) is in deploy/backup-key/ on the server: each archive is encrypted
#     with GREL's backup PUBLIC key and only the ".gpg" file is kept. The PRIVATE key never goes on this server: GREL IT keeps
#     it offline and needs it only to restore (docs/21-production-deployment.md, "Encrypted backups").
#     Without the key file the backups are still made, unencrypted, with a warning here and on GRSMS's Backups screen.
#   * Backups older than BACKUP_KEEP_DAYS (default 30) are deleted.
#   * Run one now:   docker compose -f docker-compose.prod.yml exec backup bash /backup.sh now
#
# Copy the backup folder off the server (another machine or GREL's backup system) every day.
set -uo pipefail

HOUR=${BACKUP_HOUR_GMT:-1}
KEEP=${BACKUP_KEEP_DAYS:-30}
# GREL's backup PUBLIC key: the first .asc file in deploy/backup-key/ (mounted read-only at /backup-key).
PUBKEY=""
for f in /backup-key/*.asc; do
    if [ -s "$f" ]; then PUBKEY=$f; break; fi
done

# The password goes in a private options file, so it never shows in the process list.
OPTS=$(mktemp)
chmod 600 "$OPTS"
printf '[client]\nhost=db\nuser=grsms\npassword=%s\n' "$MYSQL_PASSWORD" > "$OPTS"

# GnuPG keyring of this container only (holds GREL's PUBLIC key, nothing secret).
export GNUPGHOME=/tmp/gnupg
mkdir -p "$GNUPGHOME" && chmod 700 "$GNUPGHOME"
RECIPIENT=""
if [ -n "$PUBKEY" ] && [ -s "$PUBKEY" ]; then
    gpg --batch --quiet --import "$PUBKEY" 2>/dev/null
    # The first key's fingerprint (field 10 of the first "fpr" line), in plain bash: the slim image has no awk.
    while IFS=: read -r kind _ _ _ _ _ _ _ _ fpr _; do
        if [ "$kind" = "fpr" ]; then RECIPIENT=$fpr; break; fi
    done < <(gpg --batch --with-colons --list-keys 2>/dev/null)
fi

# Encrypts $1 to $1.gpg and removes the plain file. Without a key: leaves it as it is.
seal() {
    local plain=$1
    [ -z "$RECIPIENT" ] && { mv "$plain.part" "$plain"; return 0; }
    if gpg --batch --yes --trust-model always --recipient "$RECIPIENT" --output "$plain.gpg.part" --encrypt "$plain.part"; then
        shred -u "$plain.part" 2>/dev/null || rm -f "$plain.part"
        mv "$plain.gpg.part" "$plain.gpg"
    else
        rm -f "$plain.part" "$plain.gpg.part"
        return 1
    fi
}

backup() {
    local stamp db files keys
    stamp=$(date -u +%Y%m%d-%H%M)
    db="/backups/grsms-db-$stamp.sql.gz"
    files="/backups/grsms-files-$stamp.tar.gz"
    keys="/backups/grsms-keys-$stamp.tar.gz"

    if ! { mysqldump --defaults-extra-file="$OPTS" --single-transaction --routines --triggers --no-tablespaces grsms | gzip > "$db.part"; } || ! seal "$db"; then
        rm -f "$db.part"
        echo "$(date -u +%FT%TZ) BACKUP FAILED: database dump" >&2
        return 1
    fi

    if ! tar -czf "$files.part" -C /app-data --exclude=./keys . || ! seal "$files"; then
        rm -f "$files.part"
        echo "$(date -u +%FT%TZ) BACKUP FAILED: files archive" >&2
        return 1
    fi

    if [ -d /app-data/keys ]; then
        if ! tar -czf "$keys.part" -C /app-data keys || ! seal "$keys"; then
            rm -f "$keys.part"
            echo "$(date -u +%FT%TZ) BACKUP FAILED: keys archive" >&2
            return 1
        fi
    fi

    find /backups -maxdepth 1 -name 'grsms-*' -type f -mtime +"$KEEP" -delete
    if [ -n "$RECIPIENT" ]; then
        echo "$(date -u +%FT%TZ) backup done (encrypted for key $RECIPIENT): grsms-*-$stamp"
    else
        echo "$(date -u +%FT%TZ) backup done, NOT ENCRYPTED (no key in deploy/backup-key/): grsms-*-$stamp" >&2
    fi
}

if [ "${1:-}" = "now" ]; then
    backup
    exit $?
fi

# Phase 16: GRSMS (running as the non-root "app" user, uid 1654) leaves "back up now" requests in this folder only.
mkdir -p /backups/requests && chown 1654:1654 /backups/requests && chmod 700 /backups/requests

echo "$(date -u +%FT%TZ) GRSMS backup service started: daily after ${HOUR}:00 GMT, keeping ${KEEP} days"
if [ -n "$RECIPIENT" ]; then
    echo "$(date -u +%FT%TZ) backups are ENCRYPTED for GREL's backup key $RECIPIENT"
else
    echo "$(date -u +%FT%TZ) WARNING: backups are NOT encrypted. Put GREL's backup public key (.asc) in deploy/backup-key/ (docs/21)." >&2
fi
last=""
while true; do
    today=$(date -u +%Y%m%d)
    if [ "$(date -u +%-H)" -ge "$HOUR" ] && [ "$last" != "$today" ]; then
        backup && last=$today
    fi
    # "Back up now" on GRSMS's Backups screen (Super Admin) leaves this file; it is removed once the backup is made.
    if [ -f /backups/requests/backup-request ]; then
        echo "$(date -u +%FT%TZ) backup requested from GRSMS"
        backup && rm -f /backups/requests/backup-request
    fi
    sleep 30
done
