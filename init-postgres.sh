#!/bin/bash
set -e
service postgresql start

sudo -u postgres psql -c "ALTER USER postgres WITH PASSWORD 'postgres';"
sudo -u postgres psql -c "CREATE USER openbot WITH SUPERUSER PASSWORD 'openbot';" || sudo -u postgres psql -c "ALTER USER openbot WITH SUPERUSER PASSWORD 'openbot';"
sudo -u postgres createdb -O openbot openbot || true

PG_HBA=$(find /etc/postgresql/ -name pg_hba.conf | head -n 1)
if [ -n "$PG_HBA" ]; then
    sed -i 's/127.0.0.1\/32.*/127.0.0.1\/32 md5/' "$PG_HBA" || true
    sed -i 's/::1\/128.*/::1\/128 md5/' "$PG_HBA" || true
    # also add trust / md5 for local dev
    echo "host all all 127.0.0.1/32 trust" >> "$PG_HBA"
    echo "host all all ::1/128 trust" >> "$PG_HBA"
    service postgresql restart
fi

# Verify connection
PGPASSWORD=openbot psql -h 127.0.0.1 -U openbot -d openbot -c "SELECT 1 as connected;"
