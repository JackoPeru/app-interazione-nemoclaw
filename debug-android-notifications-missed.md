# Debug Session: android-notifications-missed
- **Status**: [OPEN]
- **Issue**: Android non riceve notifiche Hermes nonostante un cron server pubblichi un avviso ogni minuto.
- **Debug Server**: http://192.168.1.6:7777/event
- **Log File**: .dbg/trae-debug-log-android-notifications-missed.ndjson

## Reproduction Steps
1. Installare ed aprire la build Android corrente.
2. Lasciare attivo il cron Hermes che pubblica una notifica ogni minuto.
3. Attendere il polling/background worker delle notifiche.
4. Verificare se l'app recupera item da `/v1/hub/notifications` e mostra una notifica di sistema.

## Hypotheses & Verification
| ID | Hypothesis | Likelihood | Effort | Evidence |
|----|------------|------------|--------|----------|
| A | `loadGatewaySecret()` restituisce una secret sbagliata o cade sul fallback `hermes-hub`, causando auth errata. | High | Low | Pending |
| B | La secret corretta non viene mai salvata/persistita e il device continua a usare un valore vecchio. | Medium | Low | Pending |
| C | Il `WorkManager` non schedula o non esegue davvero il polling periodico delle notifiche. | High | Low | Pending |
| D | La richiesta a `/v1/hub/notifications` usa un URL/token errato o riceve una risposta HTTP inattesa. | High | Low | Pending |
| E | Gli item arrivano ma vengono filtrati come gia' visti oppure `NotificationManager` non li mostra. | Medium | Low | Pending |

## Log Evidence
Pending

## Verification Conclusion
Pending
