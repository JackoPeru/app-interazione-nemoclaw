#!/usr/bin/env bash
set -euo pipefail

attempts="${HERMES_WAIT_TAILSCALE_ATTEMPTS:-120}"
sleep_seconds="${HERMES_WAIT_TAILSCALE_SLEEP_SECONDS:-2}"

for i in $(seq 1 "$attempts"); do
  if systemctl is-active --quiet tailscaled.service && tailscale status >/dev/null 2>&1; then
    exit 0
  fi
  sleep "$sleep_seconds"
done

timeout_seconds=$((attempts * sleep_seconds))
echo "tailscale not ready after ${timeout_seconds}s" >&2
exit 1
