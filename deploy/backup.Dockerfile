# GRSMS backup service image (22 Nov 2026, docs/41 H4): the MySQL 8.4 client tools (mysqldump) plus GnuPG, so backups can be
# encrypted with GREL's backup public key. Built by docker-compose.prod.yml; the script itself is deploy/backup.sh.
FROM mysql:8.4
RUN microdnf install -y gnupg2 \
    && microdnf clean all
