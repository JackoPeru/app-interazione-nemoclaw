#!/usr/bin/env bash
set -euo pipefail

# Hermes Hub Power Monitor
# Controlla la connessione internet ogni 5 minuti.
# Se fallisce per 3 volte consecutive (15 minuti), avvia lo spegnimento per salvare la batteria dell'UPS.

MAX_FAILURES=3
CHECK_INTERVAL_SEC=300
GATEWAY_IP=$(ip route show default | awk '/default/ {print $3}' | head -n1)
TARGET_HOST="${GATEWAY_IP:-8.8.8.8}"

HERMES_API_KEY="hermes-hub"
if [ -f "$HOME/.hermes/.env" ]; then
  ENV_KEY=$(grep '^HERMES_API_KEY=' "$HOME/.hermes/.env" | cut -d '=' -f2 | tr -d '"'\''')
  if [ -n "$ENV_KEY" ]; then
    HERMES_API_KEY="$ENV_KEY"
  fi
fi
fail_count=0

echo "Avvio Hermes Power Monitor..."
echo "Target: $TARGET_HOST, Intervallo: $CHECK_INTERVAL_SEC sec, Max Fallimenti: $MAX_FAILURES"

while true; do
  if ping -c 1 -W 5 "$TARGET_HOST" >/dev/null 2>&1; then
    if [ "$fail_count" -gt 0 ]; then
      echo "$(date): Connessione ripristinata. Contatore azzerato."
    fi
    fail_count=0
  else
    fail_count=$((fail_count + 1))
    echo "$(date): Ping fallito ($fail_count/$MAX_FAILURES)"
    
    if [ "$fail_count" -ge "$MAX_FAILURES" ]; then
      echo "$(date): Assenza di rete prolungata confermata ($MAX_FAILURES fallimenti). Esecuzione shutdown!"
      # Invia una notifica al gateway locale prima di spegnere (opzionale, utile per history)
      curl -s -X POST http://127.0.0.1:8642/v1/hub/notifications \
           -H "Content-Type: application/json" \
           -H "Authorization: Bearer $HERMES_API_KEY" \
           -d '{
                 "title": "Avviso Spegnimento (UPS)",
                 "message": "Spegnimento automatico del server per presunta mancanza di corrente elettrica (15 min offline).",
                 "severity": "critical",
                 "source": "hermes-power-monitor"
               }' > /dev/null || true
               
      sudo shutdown -h now
      exit 0
    fi
  fi
  
  sleep "$CHECK_INTERVAL_SEC"
done
