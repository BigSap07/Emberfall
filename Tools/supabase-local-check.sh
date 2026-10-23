#!/usr/bin/env bash
# supabase-local-check.sh - applies every migration in Server/supabase/migrations, in number order, to a
# THROWAWAY LOCAL PostgreSQL database with stand-in Supabase roles, applies 0004 onward a second time to
# prove they are re-appliable, then runs Server/supabase/local/rules-check.sql and counts its FAIL lines.
#
# LOCAL ONLY. It never reads supabase.json, never takes a project URL or key, and connects to nothing but
# the local server psql reaches by default. It drops and recreates one database, emberfall_local_check.
#
#   bash Tools/supabase-local-check.sh            # as a user that can createdb (or run as root: it uses su postgres)
#
# Needs PostgreSQL 14 or later (sha256(), gen_random_uuid()). Exit 0 only when every file applied twice
# where it should and every check printed PASS.

set -u
root="$(cd "$(dirname "$0")/.." && pwd)"
db="emberfall_local_check"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
cp -r "$root/Server/supabase/migrations" "$root/Server/supabase/local" "$work/"
chmod -R a+rX "$work"

if [ "$(id -u)" = "0" ]; then run() { su postgres -c "$*"; }; else run() { bash -c "$*"; }; fi

run "dropdb --if-exists $db" >/dev/null 2>&1
run "createdb $db" || { echo "[local-check] could not create $db"; exit 1; }

apply() {
    local f="$1" out
    out="$(run "psql -X -q -v ON_ERROR_STOP=1 -d $db -f '$f'" 2>&1)"
    if [ $? -ne 0 ]; then
        echo "[local-check] FAILED to apply $(basename "$f")"
        echo "$out" | grep -E "ERROR|error" | head -5
        return 1
    fi
    echo "[local-check] applied $(basename "$f")"
}

# Each file from 0004 on is applied TWICE IN A ROW, which is what Server/README.md promises ("pasting one of
# them twice ... is a no-op"). Re-pasting an OLDER file after a newer one that replaced its function is not
# promised and does not hold everywhere: 0007 after 0022 fails, because 0022 widened guild_dungeon_status.
failed=0
apply "$work/local/supabase-standin.sql" || exit 1
for f in "$work"/migrations/0*.sql; do
    apply "$f" || failed=1
    n="$(basename "$f" | cut -c1-4)"
    if [ ! "$n" \< "0004" ]; then
        out="$(run "psql -X -q -v ON_ERROR_STOP=1 -d $db -f '$f'" 2>&1)" \
            && echo "[local-check] re-applied $(basename "$f") (a second paste is a no-op)" \
            || { echo "[local-check] FAILED to re-apply $(basename "$f")"; echo "$out" | grep ERROR | head -3; failed=1; }
    fi
done

out="$(run "psql -X -q -d $db -f '$work/local/rules-check.sql'" 2>&1)"
echo "$out" | grep -E "PASS:|FAIL:|ERROR" | sed -E 's/^.*(PASS|FAIL|ERROR):/\1:/'
pass=$(echo "$out" | grep -c "PASS:")
fail=$(echo "$out" | grep -c -E "FAIL:|ERROR:")
echo "[local-check] $pass passed, $fail failed"
run "dropdb --if-exists $db" >/dev/null 2>&1
[ "$failed" = "0" ] && [ "$fail" = "0" ]
