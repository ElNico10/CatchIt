#!/usr/bin/env bash
# Checks a running CatchIt web app (issue #26):
#   1. waits until /health answers and, if a version is given, reports that version (the Docker image tag),
#      so you know the new image is the one being served,
#   2. checks the app can reach its database (/health counts its products),
#   3. checks the main pages load: search, a search with results, and log in.
#
# Usage: scripts/smoke-test.sh <base-url> [expected-version] [timeout-minutes]
#   scripts/smoke-test.sh http://localhost:8080
#   scripts/smoke-test.sh https://catchit.onrender.com abc1234 25
set -euo pipefail

url="${1:?usage: smoke-test.sh <base-url> [expected-version] [timeout-minutes]}"
url="${url%/}"
expected="${2:-}"
timeout_min="${3:-10}"
deadline=$(( $(date +%s) + timeout_min * 60 ))
start=$(date +%s)

echo "Waiting for $url/health${expected:+ to report version $expected} (up to $timeout_min min)..."
while true; do
  # A free Render service that has spun down takes about a minute to wake, so a slow answer is expected.
  body="$(curl -fsS --max-time 90 "$url/health" 2>/dev/null || true)"
  version="$(printf '%s' "$body" | sed -n 's/.*"version":"\([^"]*\)".*/\1/p')"
  if [[ -n "$body" && ( -z "$expected" || "$version" == "$expected" ) ]]; then
    echo "Up: $body"
    break
  fi
  if (( $(date +%s) >= deadline )); then
    echo "::error::Timed out after $timeout_min min. Last /health answer: ${body:-none}" >&2
    exit 1
  fi
  echo "  not yet (version: ${version:-no answer}); retrying in 15 s"
  sleep 15
done

# Each check: path, text that must be on the page, description.
check() {
  local path="$1" needle="$2" what="$3" page
  if ! page="$(curl -fsS --max-time 30 "$url$path")"; then
    echo "::error::$what: $url$path did not load" >&2
    exit 1
  fi
  if ! grep -q -- "$needle" <<<"$page"; then
    echo "::error::$what: $url$path loaded but is missing $needle" >&2
    exit 1
  fi
  echo "OK  $what ($path)"
}

check "/health"          '"products":'   "Database connection"
check "/"                'id="q"'        "Search page"
check "/?q=headphones"   'id="results"'  "Search results from the database"
check "/Login"           'id="email"'    "Log-in page"

echo "All checks passed in $(( $(date +%s) - start )) s."
