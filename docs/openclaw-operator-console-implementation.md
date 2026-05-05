# ChatClaw OpenClaw Operator Console

Data: 2026-05-05

## Obiettivo

Trasformare ChatClaw da client chat a console operatore per OpenClaw.

Regola prodotto:

- No placeholder.
- Ogni bottone deve fare una chiamata reale.
- Se il Gateway non supporta il metodo, UI deve mostrare errore RPC reale.
- REST `/api/*` resta fallback legacy; control plane primario e' Gateway WebSocket.

## Contratto Primario

OpenClaw Gateway WebSocket.

Default:

```text
Gateway WS: wss://openclaw.local:8443
Protocol: 3
Role: operator
Scopes: operator.read, operator.write, operator.approvals, operator.pairing
```

Handshake:

```json
{
  "type": "req",
  "id": "...",
  "method": "connect",
  "params": {
    "minProtocol": 3,
    "maxProtocol": 3,
    "client": {
      "id": "chatclaw-windows|chatclaw-android",
      "version": "0.4.0",
      "platform": "windows|android",
      "mode": "operator"
    },
    "role": "operator",
    "scopes": ["operator.read", "operator.write", "operator.approvals", "operator.pairing"],
    "auth": { "token": "<saved-secret>" }
  }
}
```

## Sicurezza Credenziali

Windows:

- Token/password Gateway in Windows Credential Locker.
- Campo vuoto mantiene credenziale esistente.
- Reset elimina credenziale.

Android:

- Token/password Gateway cifrato con Android Keystore AES-GCM.
- Ciphertext in `chatclaw_settings`.
- Campo vuoto mantiene credenziale esistente.
- Reset elimina credenziale.

## Superfici da Implementare

### 1. Connessione Gateway

Funzioni reali:

- Test connessione WS.
- Handshake `connect`.
- Stato errore live.
- RPC manuale per debug.
- Credenziali salvate sicure.

Metodi:

- `status`
- `system-presence`

### 2. Dashboard Server

Funzioni reali:

- Stato gateway.
- Presenza sistema/nodi.
- Stato modelli.
- Stato cron.
- Log recenti.

Metodi:

- `status`
- `system-presence`
- `models.status`
- `cron.status`
- `logs.tail`

### 3. Modelli e Provider

Funzioni reali:

- Stato modello/provider.
- Lista modelli.
- Set modello.
- Scan provider.

Metodi:

- `models.status`
- `models.list`
- `models.set`
- `models.scan`

### 4. Plugin e Skills

Funzioni reali:

- Lista plugin.
- Info plugin.
- Enable/disable.
- Doctor.
- Update.

Metodi:

- `plugins.list`
- `plugins.info`
- `plugins.enable`
- `plugins.disable`
- `plugins.doctor`
- `plugins.update`

Regola sicurezza:

- Install plugin non deve essere one-click cieco.
- Richiede origine, versione/pin, permessi, rischio, conferma.

### 5. Approvazioni Exec

Funzioni reali:

- Leggi pending approvals.
- Approva una volta.
- Approva sempre.
- Nega.

Metodi/eventi:

- evento `exec.approval.requested`
- `exec.approval.resolve`
- `exec.approvals.get`

Payload decisioni:

```json
{ "id": "...", "decision": "allow-once" }
{ "id": "...", "decision": "allow-always" }
{ "id": "...", "decision": "deny" }
```

### 6. Workspace e File

Funzioni reali:

- Usare solo root consentite.
- Lista workspace.
- Upload/download solo via Gateway o Admin Bridge.
- No accesso filesystem libero dal client.

Metodi previsti:

- `workspace.list`
- `workspace.files.list`
- `workspace.files.read`
- `workspace.files.write`
- `workspace.snapshot`

Se Gateway non espone questi metodi, usare Admin Bridge.

### 7. Config Sicura

Funzioni reali:

- `config.get`.
- Patch JSON5 controllata.
- `baseHash` obbligatorio.
- `config.patch`.
- `config.apply` solo avanzato.

Metodi:

- `config.get`
- `config.patch`
- `config.apply`

Regola:

- Prima leggere config.
- Poi mostrare patch.
- Poi applicare.

### 8. Log, Trace, Audit

Funzioni reali:

- Tail log.
- Ultimi errori.
- Audit eventi operatore.

Metodi:

- `logs.tail`
- `status`
- `security.audit`

### 9. Canali

Funzioni reali:

- Stato canali.
- Lista canali.
- Login/logout quando Gateway lo espone.
- Test messaggio.

Metodi:

- `channels.status`
- `channels.list`
- `channels.login`
- `channels.logout`
- `message.send`

### 10. Cron e Automazioni

Funzioni reali:

- Lista job.
- Stato scheduler.
- Aggiungi job.
- Run manuale.
- Rimuovi job.
- Storico run.

Metodi:

- `cron.status`
- `cron.list`
- `cron.add`
- `cron.update`
- `cron.remove`
- `cron.run`
- `cron.runs`

### 11. Nodi/Host

Funzioni reali:

- Lista nodi.
- Presenza.
- Exec approvals per nodo quando supportato.

Metodi:

- `nodes.list`
- `system-presence`
- `system.execApprovals.get`
- `system.execApprovals.set`

### 12. Terminale Remoto Blindato

Funzioni reali:

- Non terminale libero di default.
- Preset sicuri.
- Esecuzione via OpenClaw exec con approvazioni.
- Log obbligatorio.

Metodi:

- `system.run` solo se Gateway autorizza.
- Meglio Admin Bridge per preset macchina.

### 13. Security Center

Funzioni reali:

- Audit sicurezza.
- Stato esposizione gateway.
- Stato approvals.
- Tool pericolosi.
- Plugin non verificati.

Metodi:

- `security.audit`
- `status`
- `exec.approvals.get`
- `plugins.list`

### 14. Memory/Context

Funzioni reali:

- Stato memoria.
- Search.
- Reindex.

Metodi:

- `memory.status`
- `memory.search`
- `memory.index`

### 15. Secrets Manager

Funzioni reali:

- Reload secrets.
- Audit secrets.
- Non mostrare valori segreti.

Metodi:

- `secrets.reload`
- `secrets.audit`

### 16. Update Manager Stack

Funzioni reali:

- Update OpenClaw.
- Update plugin.
- Update ChatClaw.

Metodi:

- `update.run`
- `plugins.update`

### 17. Action Builder

Funzioni reali:

- Creare automazioni usando cron + system event.
- No motore locale parallelo inutile.

Metodi:

- `cron.add`
- `cron.update`
- `cron.remove`
- `system.event`

### 18. Admin Bridge

Serve solo se Gateway non copre operazioni macchina.

Operazioni permesse:

- Status macchina.
- Tail log file consentiti.
- Doctor.
- Security audit.
- Restart servizi.
- Update stack.

Regole:

- Token obbligatorio.
- Allowlist comandi.
- Nessun shell libero senza policy.
- Output e audit sempre loggati.

## Piano Implementazione

Fase 1:

- Gateway RPC client riusabile.
- Console operatore manuale + preset reali.
- Stesso set Windows/Android.

Fase 2:

- Pagine verticali dedicate usando stessi metodi RPC.
- Approvals realtime con eventi WS.
- Config patch con diff.

Fase 3:

- Admin Bridge.
- File/workspace manager.
- Terminale preset blindato.
- Security center completo.

Fase 4:

- Test runtime con Gateway reale.
- Release solo dopo build, scan codice, prova update.

## Definition of Done

Una funzione e' completa solo se:

- chiama un metodo Gateway/Admin Bridge reale;
- gestisce successo, errore, timeout e metodo non supportato;
- non salva segreti in chiaro;
- non esegue azioni distruttive senza conferma lato Gateway/approval;
- esiste su Windows e Android;
- build passa.

## Stato Dopo Questa Milestone

Implementare ora:

- Fase 1 completa: RPC manuale + preset Gateway WS.
- Fase 2 completa come base operativa: approvals, config patch/apply, workspace file RPC, sezioni dedicate nella console.
- Fase 3 completa come base operativa: `ChatClaw.AdminBridge` reale con auth bearer, allowlist comandi, root filesystem consentite, audit log, status, doctor, security audit, update, restart configurabile, tail log, list/read/write file.
- Niente release finche' build finale, scan codice e decisione Matteo su pubblicazione.

## ChatClaw Admin Bridge

Progetto:

```text
src/ChatClaw.AdminBridge
```

Build:

```powershell
dotnet build .\src\ChatClaw.AdminBridge\ChatClaw.AdminBridge.csproj -c Release
```

Avvio esempio:

```powershell
$env:CHATCLAW_ADMIN_TOKEN='token-lungo'
$env:CHATCLAW_ADMIN_ROOTS='C:\openclaw-workspace'
$env:CHATCLAW_RESTART_GATEWAY_COMMAND='systemctl restart openclaw-gateway'
dotnet run --project .\src\ChatClaw.AdminBridge\ChatClaw.AdminBridge.csproj --urls http://0.0.0.0:9443
```

Endpoint:

- `GET /v1/health`
- `GET /v1/status`
- `POST /v1/actions/doctor`
- `POST /v1/actions/security-audit`
- `POST /v1/actions/update-openclaw`
- `POST /v1/actions/plugin-list`
- `POST /v1/actions/restart-gateway` solo se `CHATCLAW_RESTART_GATEWAY_COMMAND` e' impostato
- `POST /v1/logs/tail`
- `POST /v1/files/list`
- `POST /v1/files/read`
- `POST /v1/files/write`

Sicurezza:

- Tutto richiede `Authorization: Bearer <token>` tranne `/v1/health`.
- Comandi solo allowlist.
- File access solo sotto `CHATCLAW_ADMIN_ROOTS`.
- Scrittura file crea backup `.bak`.
- Audit append-only in `~/.chatclaw-admin-bridge/audit.log` o `CHATCLAW_ADMIN_AUDIT`.
