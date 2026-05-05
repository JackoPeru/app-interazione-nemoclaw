# Guida app Android per NemoClaw

Data: 2026-05-03

## Obiettivo

Creare app Android stile ChatGPT per comunicare con home-server NemoClaw/local AI. App deve funzionare bene da telefono, con:

- Chat normale.
- Modalita agente/ordini.
- Streaming risposta.
- Notifiche task.
- Pairing sicuro con server.
- UI touch-first, leggibile, veloce.

## Decisione tecnica

Stack consigliato: Kotlin + Jetpack Compose + Android Architecture Components.

Motivo:

- Compose e' UI moderna nativa Android.
- ViewModel, StateFlow, coroutines e repository pattern sono baseline architettura Android attuale.
- Nativo Android gestisce meglio lifecycle, background, notifiche, rete mobile, biometria, storage sicuro.
- Per app chat/task con streaming e notifiche, controllo nativo vale piu' del riuso totale UI.

Non usare WebView come app principale: peggiora offline, notifiche, sicurezza credenziali, UX mobile. Non usare MAUI come default: riduce doppio codice, ma Compose offre migliore controllo Android e maggiore coerenza con linee guida Android.

## Architettura

Android app parla solo con `Nemoclaw Gateway API`, non con NemoClaw CLI, Ollama proxy o OpenShell direttamente.

```text
Android App
  -> OpenClaw Gateway WebSocket control plane
  -> REST/SSE fallback legacy
OpenClaw Gateway su home-server
  -> NemoClaw/OpenShell/local inference
```

Vantaggi:

- Stesso backend della app Windows.
- Token e credenziali sensibili restano server-side.
- API stabile anche se NemoClaw cambia.
- Audit e approvazioni task centralizzate.

## Gateway richiesto

Contratto primario: WebSocket Gateway OpenClaw ufficiale.

- URL: `ws://` o `wss://`, default `wss://openclaw.local:8443`.
- Primo frame client: RPC `connect` con `minProtocol=3`, `maxProtocol=3`, ruolo `operator`, scopes operatore e `auth.token` se presente.
- RPC base usate dal client: `status`, `system-presence`, `models.status`, `models.list`, `plugins.list`, `channels.status`, `nodes.list`, `exec.approvals.get`.
- Approvals: richieste reali da evento `exec.approval.requested`, risoluzione futura con `exec.approval.resolve`.

Contratto REST legacy/fallback:

Endpoint minimi uguali a Windows:

```http
GET  /api/health
GET  /api/server/status
GET  /api/models
GET  /api/conversations
POST /api/conversations
GET  /api/conversations/{conversationId}/messages
POST /api/conversations/{conversationId}/messages
GET  /api/conversations/{conversationId}/stream
POST /api/tasks
GET  /api/tasks/{taskId}
GET  /api/tasks/{taskId}/events
POST /api/tasks/{taskId}/approve
POST /api/tasks/{taskId}/deny
POST /api/tasks/{taskId}/cancel
```

Streaming:

- SSE se comunicazione e' server -> client.
- WebSocket solo se serve controllo bidirezionale realtime continuo.
- Client deve supportare resume con `Last-Event-ID` o `sinceEventId`.

## Autenticazione e pairing

Flusso consigliato:

1. Server mostra pairing code o QR.
2. App scansiona QR o inserisce codice.
3. Gateway emette device token + refresh token.
4. Token salvati in Android Keystore/EncryptedSharedPreferences.
5. Access token breve, refresh token revocabile.
6. Biometria opzionale per aprire app o sbloccare token.

No:

- Token NemoClaw/Ollama nel telefono.
- Password server salvata in chiaro.
- HTTP non cifrato in build release.

## Rete Android

Accesso consigliato:

- Tailscale/WireGuard/VPN per fuori casa.
- LAN privata per uso domestico.
- HTTPS con certificato valido o CA locale installata.

Android:

- Configurare Network Security Configuration.
- Permettere cleartext solo in debug e solo per host locale/test.
- In release, `cleartextTrafficPermitted=false`.
- Timeout lunghi per inferenza locale lenta.
- Retry con backoff solo per errori rete, non per POST gia' accettati senza idempotency key.

## UI mobile

Navigazione:

- Bottom navigation o navigation rail adattiva: Chat, Task, Server, Settings.
- Chat list separata da conversation screen.
- Pannelli secondari diventano bottom sheet, non colonne strette.

Conversation screen:

- Header con titolo chat, modello, stato connessione.
- `LazyColumn` per messaggi.
- Composer fisso in basso.
- Pulsante stop quando streaming attivo.
- Long press su messaggio: copy, retry, delete local/export.
- Markdown e code block con copy.

Task screen:

- Timeline eventi verticale.
- Card per comando/tool/network request.
- Approve/Deny grandi e chiari.
- Stato persistente se app va in background.
- Notifica quando task richiede approvazione o completa.

Server screen:

- Stato gateway.
- Stato NemoClaw sandbox.
- Stato inferenza/modello.
- Ultimo ping.
- Log recenti non sensibili.

## Architettura Android interna

```text
app/
  ui/
    chat/
    tasks/
    server/
    settings/
  domain/
    SendMessageUseCase
    StartTaskUseCase
    ApproveTaskUseCase
  data/
    api/
      NemoclawApi.kt
      StreamingClient.kt
    db/
      AppDatabase.kt
      ConversationDao.kt
    repository/
      ChatRepository.kt
      TaskRepository.kt
    security/
      TokenStore.kt
```

Pattern:

- UI layer: composable stateless dove possibile.
- ViewModel per screen state e business interaction.
- `StateFlow` per stato UI.
- Repository per rete/cache.
- Room per cache locale.
- WorkManager per sync/notifications leggere.
- Hilt/Koin opzionale per DI; partire semplice se progetto piccolo.

## Stato Compose

Regole:

- Screen state in ViewModel.
- Stato UI semplice vicino al composable.
- `LazyListState` hoistato a livello screen quando serve scroll-to-bottom.
- Eventi one-shot tramite channel/shared flow controllato.
- Nessun `ViewModel` passato a composable figli profondi: passare state + callbacks.

Esempio stati:

```text
ChatUiState:
  isLoading, isStreaming, connectionState, conversations, selectedConversation,
  messages, draft, error, activeModel

TaskUiState:
  tasks, selectedTask, events, approvalRequest, isCancelling, error
```

## Persistenza locale

Room:

- Conversazioni recenti.
- Messaggi cache.
- Draft.
- Server config.
- Eventi task recenti.

Non salvare:

- Token in Room.
- Segreti in log.
- Allegati sensibili senza scelta esplicita utente.

Sync:

- Server source of truth.
- Cache invalidata con `updatedAt` o event id.
- Se offline, mostra ultimo stato + banner.

## Notifiche

Usare notifiche Android per:

- Task completato.
- Task fallito.
- Richiesta approvazione.
- Server offline dopo task in corso.

Non notificare ogni token/messaggio streaming.

Background:

- Android limita lavoro continuo in background.
- Per task lunghi, backend continua; app riceve stato quando riaperta o via push/local polling controllato.
- Se non c'e' push service esterno, usare polling leggero solo quando task attivo e app in foreground.

## Sicurezza task/agente

Regole UI:

- Distinguere chiaramente chat testo e azioni agente.
- Mostrare comando/path/host/metodo prima di approve.
- Approve con contesto: cosa cambia, rischio, scadenza.
- Deny sempre disponibile.
- Cancel sempre visibile per task running.

Regole server da rispettare:

- NemoClaw ha policy deny-by-default per rete.
- Endpoint agentici vanno approvati con scope minimo.
- Preset larghi non vanno abilitati senza motivo.
- Client mobile non deve aggirare policy server.

## Piano sviluppo

Fase 0: Specifica condivisa

- OpenAPI gateway.
- Event schema.
- Pairing/auth.
- Error model.

Fase 1: Skeleton Android

- Progetto Kotlin/Compose.
- Navigation.
- Tema Material 3.
- Settings server URL.
- Health check.

Fase 2: Auth

- Pairing code/QR.
- TokenStore sicuro.
- Refresh token.
- Logout/revoke.

Fase 3: Chat

- Lista conversazioni.
- Thread messaggi.
- Composer.
- Streaming SSE.
- Stop/retry.
- Cache Room.

Fase 4: Task/agente

- Start task.
- Timeline eventi.
- Approval flow.
- Cancel.
- Notifications.

Fase 5: Hardening mobile

- Offline/reconnect.
- Process death restore.
- Rotazione schermo.
- Rete mobile instabile.
- Certificato/CA.
- Accessibility font scale/screen reader.

## Test minimi

- Server non raggiungibile.
- VPN spenta/accesa.
- Certificato non valido.
- Token scaduto.
- App in background durante streaming.
- Rotazione schermo durante streaming.
- Android process death e restore.
- Task richiede approvazione mentre app chiusa.
- Conversazione lunga.
- Font scale 200%.
- Tema dark/light.
- Cleartext bloccato in release.

## Decisioni aperte

- App deve funzionare fuori casa? Se si, scegliere VPN/Tailscale prima del codice.
- Serve push notification vero? Se si, serve servizio push o relay.
- Storico chat deve essere cifrato localmente oltre Keystore token?
- Allegati/file: quali tipi permessi?
- Comandi agente possono modificare home-server o solo sandbox?

## Fonti

- Android app architecture: https://developer.android.com/topic/architecture
- Jetpack Compose state: https://developer.android.com/develop/ui/compose/state
- Jetpack Compose state hoisting: https://developer.android.com/develop/ui/compose/state-hoisting
- Android network security config: https://developer.android.com/privacy-and-security/security-config
- Android network connections/security: https://developer.android.com/develop/connectivity/network-ops/connecting
- NVIDIA NemoClaw local inference: https://docs.nvidia.com/nemoclaw/0.0.26/inference/use-local-inference.html
- NVIDIA NemoClaw security best practices: https://docs.nvidia.com/nemoclaw/0.0.26/security/best-practices.html
- NVIDIA NemoClaw network policies: https://docs.nvidia.com/nemoclaw/0.0.26/reference/network-policies.html
