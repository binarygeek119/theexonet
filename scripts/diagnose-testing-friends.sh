#!/bin/bash
# Check admin testing-mode flags and whether dummy friends are returned by the API.
#
# Host mode (on the server, no token):
#   sudo diagnose-rava-testing-friends
#   sudo diagnose-rava-testing-friends --enable
#   bash scripts/diagnose-testing-friends.sh --enable
#
# Remote/API mode (with a game session token):
#   bash scripts/diagnose-testing-friends.sh <bearer-token>
#   bash scripts/diagnose-testing-friends.sh <bearer-token> https://ravaapi.example.com
#   RAVA_DIAG_TOKEN=... bash scripts/diagnose-testing-friends.sh
#
# API mode via login (optional):
#   RAVA_DIAG_USERNAME=admin RAVA_DIAG_PASSWORD=secret bash scripts/diagnose-testing-friends.sh
#
# Game token (browser console): localStorage.getItem('rava_token')
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$(readlink -f "${BASH_SOURCE[0]}")")" && pwd)"
# shellcheck source=rava-hosting-env.sh
[ -f "${SCRIPT_DIR}/rava-hosting-env.sh" ] && source "${SCRIPT_DIR}/rava-hosting-env.sh"

PUBLISH_DIR="${RAVA_PUBLISH_DIR:-/var/www/publish}"
DATA_DIR="${RAVA_DATA_DIR:-/var/www/data}"
API_INTERNAL="${RAVA_API_INTERNAL_URL:-http://127.0.0.1:5000}"
API_PUBLIC="${RAVA_API_PUBLIC_URL:-https://ravaapi.binarygeek119.duckdns.org}"

usage() {
  echo "Usage:" >&2
  echo "  $0 [--enable|--disable]                      # host mode (DB + config, no token)" >&2
  echo "  $0 <bearer-token> [api-base-url]            # API checks with a game JWT" >&2
  echo "  RAVA_DIAG_TOKEN=... $0 [api-base-url]" >&2
  echo "  RAVA_DIAG_USERNAME=... RAVA_DIAG_PASSWORD=... $0 [api-base-url]" >&2
  echo >&2
  echo "  --enable   Set AdminTestingModeEnabled=true for configured admin players (host mode)" >&2
  echo "  --disable  Set AdminTestingModeEnabled=false for configured admin players (host mode)" >&2
}

HOST_ENABLE=""
HOST_DISABLE=""
CLI_ARGS=()
for arg in "$@"; do
  case "$arg" in
    --enable)
      HOST_ENABLE=1
      ;;
    --disable)
      HOST_DISABLE=1
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      CLI_ARGS+=("$arg")
      ;;
  esac
done

if [ -n "$HOST_ENABLE" ] && [ -n "$HOST_DISABLE" ]; then
  echo "ERROR: use only one of --enable or --disable." >&2
  exit 1
fi

TOKEN="${RAVA_DIAG_TOKEN:-${CLI_ARGS[0]:-}}"
API_BASE="${CLI_ARGS[1]:-${RAVA_DIAG_API_BASE:-$API_PUBLIC}}"
API_BASE="${API_BASE%/}"

require_python3() {
  if ! command -v python3 >/dev/null 2>&1; then
    echo "ERROR: python3 is required." >&2
    exit 1
  fi
}

resolve_appsettings_path() {
  if [ -f "${DATA_DIR}/appsettings.json" ]; then
    printf '%s' "${DATA_DIR}/appsettings.json"
  elif [ -f "${PUBLISH_DIR}/appsettings.json" ]; then
    printf '%s' "${PUBLISH_DIR}/appsettings.json"
  else
    printf ''
  fi
}

run_host_diagnostics() {
  require_python3

  local appsettings
  appsettings="$(resolve_appsettings_path)"

  echo "=== theexonet testing friends diagnostics (host) ==="
  echo "Publish dir: ${PUBLISH_DIR}"
  echo "Data dir:    ${DATA_DIR}"
  echo "API internal:${API_INTERNAL}"
  echo

  python3 - "$appsettings" "$PUBLISH_DIR" "$DATA_DIR" "$API_INTERNAL" "${HOST_ENABLE:-}" "${HOST_DISABLE:-}" <<'PY'
import json
import os
import subprocess
import sys
from pathlib import Path
from shutil import which
from urllib import error, request

appsettings_path = sys.argv[1]
publish_dir = Path(sys.argv[2])
data_dir = Path(sys.argv[3])
api_internal = sys.argv[4].rstrip("/")
host_enable = sys.argv[5] == "1"
host_disable = sys.argv[6] == "1"

def parse_connection_string(raw):
    parts = {}
    for item in raw.split(";"):
        if "=" not in item:
            continue
        key, value = item.split("=", 1)
        parts[key.strip().lower()] = value.strip()
    return parts

def load_appsettings(path):
    if not path:
        print("ERROR: appsettings.json not found.")
        print(f"  Expected: {data_dir / 'appsettings.json'}")
        print("  Run: sudo migrate-rava-data")
        return None
    try:
        return json.loads(Path(path).read_text(encoding="utf-8"))
    except json.JSONDecodeError as exc:
        print(f"ERROR: invalid JSON in {path}: {exc}")
        return None

def check_api_health():
    url = f"{api_internal}/api/status"
    try:
        with request.urlopen(url, timeout=5) as response:
            print(f"API health ({url}): HTTP {response.status}")
            return True
    except error.HTTPError as exc:
        print(f"API health ({url}): HTTP {exc.code}")
        return False
    except Exception as exc:
        print(f"API health ({url}): unreachable ({exc})")
        return False

def check_publish_dll():
    dll = publish_dir / "Rava.Api.dll"
    infra = publish_dir / "Rava.Infrastructure.dll"
    if dll.is_file():
        print(f"OK  {dll}")
    else:
        print(f"MISSING  {dll}")
    if infra.is_file():
        print(f"OK  {infra}")
    else:
        print(f"MISSING  {infra}")

settings = load_appsettings(appsettings_path)
if settings is None:
    sys.exit(1)

admin_usernames = settings.get("Admin", {}).get("Usernames") or []
print("--- Admin:Usernames ---")
if admin_usernames:
    for name in admin_usernames:
        print(f"  - {name}")
else:
    print("  (none configured)")

print()
print("--- publish ---")
check_publish_dll()

print()
print("--- API ---")
check_api_health()

conn_raw = (settings.get("ConnectionStrings") or {}).get("DefaultConnection") or ""
if not conn_raw:
    print()
    print("ERROR: ConnectionStrings:DefaultConnection missing from appsettings.json")
    sys.exit(1)

conn = parse_connection_string(conn_raw)
if not conn.get("database"):
    print()
    print("ERROR: could not parse database name from DefaultConnection")
    sys.exit(1)

if not which("psql"):
    print()
    print("WARN: psql not installed — skipping database checks.")
    print("Install postgresql-client or pass a bearer token for API-only checks:")
    print("  RAVA_DIAG_USERNAME=... RAVA_DIAG_PASSWORD=... diagnose-rava-testing-friends")
    sys.exit(0)

admin_filter = ""
if admin_usernames:
    admin_filter = f' AND lower("Username") IN ({", ".join(repr(name.lower()) for name in admin_usernames)})'

env = os.environ.copy()
if conn.get("password"):
    env["PGPASSWORD"] = conn["password"]

psql_base = [
    "psql",
    "-h", conn.get("host", "localhost"),
    "-p", conn.get("port", "5432"),
    "-U", conn.get("username", "postgres"),
    "-d", conn["database"],
    "-v", "ON_ERROR_STOP=1",
]

def run_sql(sql):
    result = subprocess.run(
        psql_base + ["-At", "-F", "|", "-c", sql],
        env=env,
        capture_output=True,
        text=True,
    )
    return result

print()
print("--- database ---")

column_check = run_sql(
    """
    SELECT EXISTS (
      SELECT 1
      FROM information_schema.columns
      WHERE table_name = 'Players'
        AND column_name = 'AdminTestingModeEnabled'
    );
    """
)
if column_check.returncode != 0:
    print(f"ERROR: database query failed: {column_check.stderr.strip()}")
    sys.exit(1)

has_column = column_check.stdout.strip() == "t"
if has_column:
    print('OK  "Players"."AdminTestingModeEnabled" column exists')
else:
    print('MISSING  "Players"."AdminTestingModeEnabled" column')
    print("  Restart the API so DatabaseSchemaUpdater can migrate the schema.")

if not admin_usernames:
    print()
    print("No admin usernames configured — nothing else to check in DB.")
    sys.exit(0)

if not has_column:
    sys.exit(1)

if host_enable or host_disable:
    target = "true" if host_enable else "false"
    action = "ON" if host_enable else "off"
    username_filter = ", ".join(repr(name.lower()) for name in admin_usernames)
    update = run_sql(
        f"""
        UPDATE "Players"
        SET "AdminTestingModeEnabled" = {target}
        WHERE lower("Username") IN ({username_filter});
        """
    )
    if update.returncode != 0:
        print(f"ERROR: failed to set testing mode {action}: {update.stderr.strip()}")
        sys.exit(1)
    print(f"Set testing mode {action} for configured admin player(s).")

player_query = f"""
SELECT "Username", "AdminTestingModeEnabled"
FROM "Players"
WHERE 1=1{admin_filter}
ORDER BY "Username";
"""
players = run_sql(player_query)
if players.returncode != 0:
    print(f"ERROR: player query failed: {players.stderr.strip()}")
    sys.exit(1)

rows = [line for line in players.stdout.splitlines() if line.strip()]
print()
print("Admin players in database:")
if not rows:
    print("  (no matching player rows — admin accounts may not exist yet)")
else:
    enabled_any = False
    for row in rows:
        username, enabled = row.split("|", 1)
        flag = enabled.lower() == "t"
        enabled_any = enabled_any or flag
        state = "ON" if flag else "off"
        print(f"  - {username}: testing mode {state}")
    print()
    if enabled_any:
        print("Expected in-game: 12 dummy friends when logged in as an admin with testing mode ON.")
        print("If Friends is still empty after API redeploy, run API checks with a JWT:")
        print("  RAVA_DIAG_USERNAME=<admin> RAVA_DIAG_PASSWORD=... diagnose-rava-testing-friends")
    else:
        print("Testing mode is OFF for all configured admin players.")
        print("Enable it in the admin portal (Testing page), or on the host run:")
        print("  sudo diagnose-rava-testing-friends --enable")
        print("Then re-run this script and hard-refresh the game.")
PY
}

login_for_token() {
  local username="$1"
  local password="$2"
  local base="${3:-$API_INTERNAL}"
  local body status

  if ! command -v curl >/dev/null 2>&1; then
    echo "ERROR: curl is required for login." >&2
    return 1
  fi

  body="$(python3 - "$username" "$password" <<'PY'
import json, sys
print(json.dumps({"username": sys.argv[1], "password": sys.argv[2]}))
PY
)"
  status="$(
    curl -sS -o "${tmpdir}/login.json" -w '%{http_code}' \
      -H "Content-Type: application/json" \
      -H "Accept: application/json" \
      -X POST \
      -d "$body" \
      "${base%/}/api/auth/login" 2>/dev/null || echo "000"
  )"

  if [ "$status" != "200" ]; then
    echo "ERROR: login failed (HTTP ${status}). Check RAVA_DIAG_USERNAME / RAVA_DIAG_PASSWORD." >&2
    return 1
  fi

  python3 - "${tmpdir}/login.json" <<'PY'
import json, sys
from pathlib import Path
data = json.loads(Path(sys.argv[1]).read_text(encoding="utf-8"))
token = data.get("token")
if not token:
    raise SystemExit("ERROR: login response did not include token")
print(token)
PY
}

run_api_diagnostics() {
  require_python3

  if ! command -v curl >/dev/null 2>&1; then
    echo "ERROR: curl is required." >&2
    exit 1
  fi

  tmpdir="$(mktemp -d)"
  trap 'rm -rf "$tmpdir"' EXIT

  fetch() {
    local name="$1"
    local path="$2"
    local status

    status="$(
      curl -sS -o "${tmpdir}/${name}.json" -w '%{http_code}' \
        -H "Authorization: Bearer ${TOKEN}" \
        -H "Accept: application/json" \
        "${API_BASE}${path}" 2>/dev/null || echo "000"
    )"
    printf '%s' "$status" > "${tmpdir}/${name}.status"
  }

  fetch access /api/admin/access
  fetch friends /api/player/friends
  fetch profile /api/player/profile

  python3 - "$tmpdir" "$API_BASE" <<'PY'
import json
import sys
from pathlib import Path

tmpdir = Path(sys.argv[1])
api_base = sys.argv[2]

def load(name):
    status = (tmpdir / f"{name}.status").read_text(encoding="utf-8").strip()
    body = (tmpdir / f"{name}.json").read_text(encoding="utf-8")
    if status == "000":
        return {"error": "request failed", "httpStatus": status}
    try:
        parsed = json.loads(body or "{}")
    except json.JSONDecodeError:
        return {"error": "invalid json", "httpStatus": status, "body": body[:200]}
    if status and not status.startswith("2"):
        parsed.setdefault("httpStatus", status)
    return parsed

access = load("access")
friends = load("friends")
profile = load("profile")

friend_list = friends.get("friends") or []
testing_dummies = [friend for friend in friend_list if friend.get("isTestingDummy")]

print("=== theexonet testing friends diagnostics (API) ===")
print(f"API base:              {api_base}")
print(f"admin/access HTTP:     {(tmpdir / 'access.status').read_text(encoding='utf-8').strip()}")
print(f"player/friends HTTP:   {(tmpdir / 'friends.status').read_text(encoding='utf-8').strip()}")
print(f"player/profile HTTP:   {(tmpdir / 'profile.status').read_text(encoding='utf-8').strip()}")
print()
print(f"isAdmin:               {access.get('isAdmin')}")
print(f"testingModeEnabled:    {access.get('testingModeEnabled')}")
print(f"profileIsStaffAdmin:   {profile.get('isStaffAdmin')}")
print(f"profileTestingMode:    {profile.get('testingModeEnabled')}")
print(f"friendCount:           {len(friend_list)}")
print(f"testingDummyCount:     {len(testing_dummies)}")
print()
print("sampleFriends:")
for friend in friend_list[:5]:
    print(f"  - {friend.get('username', '?')} (isTestingDummy={friend.get('isTestingDummy', False)})")

errors = []
for label, payload in (
    ("admin/access", access),
    ("player/friends", friends),
    ("player/profile", profile),
):
    if payload.get("error"):
        errors.append(f"  {label}: {payload}")

if errors:
    print()
    print("errors:")
    print("\n".join(errors))

if access.get("isAdmin") and access.get("testingModeEnabled") and len(testing_dummies) == 0:
    print()
    print("WARN: testing mode is ON but API returned no isTestingDummy friends.")
    print("     Redeploy/restart rava-api so server-side dummy merge is live.")
PY
}

if [ -n "$HOST_ENABLE" ] || [ -n "$HOST_DISABLE" ]; then
  run_host_diagnostics
  exit 0
fi

if [ -z "$TOKEN" ] && [ -n "${RAVA_DIAG_USERNAME:-}" ] && [ -n "${RAVA_DIAG_PASSWORD:-}" ]; then
  tmpdir="$(mktemp -d)"
  trap 'rm -rf "$tmpdir"' EXIT
  login_base="${RAVA_DIAG_API_BASE:-$API_INTERNAL}"
  TOKEN="$(login_for_token "$RAVA_DIAG_USERNAME" "$RAVA_DIAG_PASSWORD" "$login_base")"
  echo "Logged in as ${RAVA_DIAG_USERNAME} via ${login_base}"
  echo
  run_api_diagnostics
  exit 0
fi

if [ -z "$TOKEN" ]; then
  run_host_diagnostics
  exit 0
fi

run_api_diagnostics
