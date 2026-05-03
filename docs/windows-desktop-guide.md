# Guida app Windows desktop per NemoClaw

Data: 2026-05-03

## Obiettivo

Creare client desktop Windows stile ChatGPT per parlare con home-server che ospita NemoClaw e AI locale. App deve supportare:

- Chat normale con modello locale.
- Ordini operativi all'agente NemoClaw.
- Streaming risposta token/eventi.
- Storico conversazioni.
- Stato server/sandbox/modello.
- Conferme esplicite per azioni rischiose.

## Decisione tecnica

Stack consigliato: WinUI 3 + Windows App SDK + C#/.NET, packaged MSIX.

Motivo:

- WinUI 3 e' framework nativo moderno Microsoft per app desktop Windows.
- Windows App SDK offre API moderne, Fluent UI, notifiche, windowing, deployment pulito.
- App packaged semplifica installazione, update, notifiche, background task e futuro Store/private distribution.
- C# permette client robusto, async streaming, SQLite locale, integrazione Windows Credential Manager.

Non usare Electron per prima versione: piu' pesante, UX meno nativa, piu' superficie sicurezza. Non usare MAUI come default: utile per condividere codice con Android, ma UI chat complessa e differenze piattaforma rendono nativo WinUI piu' controllabile.

## Architettura

L'app Windows non deve parlare direttamente con CLI/sandbox NemoClaw. Serve un servizio stabile sul server:

```text
Windows App
  -> HTTPS/SSE/WebSocket
Nemoclaw Gateway API su home-server
  -> NemoClaw CLI/OpenShell/local inference
  -> Ollama/vLLM/OpenAI-compatible local endpoint
```

Perche gateway:

- NemoClaw e' alpha: API/comportamenti possono cambiare.
- Il token/proxy locale NemoClaw non deve stare nel client.
- Android e Windows usano stesso contratto API.
- Audit, autorizzazioni, rate limit, pairing e policy restano lato server.

## Gateway richiesto

Contratto minimo che la app si aspetta:

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

- Usare SSE per token/eventi se basta server -> client.
- Usare WebSocket solo se serve canale bidirezionale lungo.
- Payload eventi NDJSON/SSE con `event`, `id`, `conversationId`, `taskId`, `role`, `contentDelta`, `toolCall`, `status`, `createdAt`.

Auth:

- Primo setup con pairing code mostrato sul server.
- Client riceve refresh token/device token.
- Token salvato in Windows Credential Manager.
- Ogni richiesta usa `Authorization: Bearer <access_token>`.

Rete:

- Preferire Tailscale/WireGuard/VPN o LAN privata.
- Evitare port forwarding pubblico.
- HTTPS obbligatorio. Per certificato self-signed, installare CA locale fidata su Windows.

## Modalita prodotto

### Chat normale

Flusso:

1. Utente scrive prompt.
2. Client crea messaggio `user`.
3. Gateway invia a modello locale/OpenAI-compatible endpoint.
4. Client mostra streaming token.
5. Storico resta su server; cache locale opzionale.

Controlli UI:

- Selettore modello.
- Selettore temperatura/profilo solo in impostazioni avanzate.
- Stop generation.
- Retry/regenerate.
- Copy message.
- Cerca nella conversazione.

### Ordini/agente

Flusso:

1. Utente sceglie modalita `Agente`.
2. Client invia task con obiettivo, limiti, workspace target.
3. Gateway avvia NemoClaw/OpenShell sandbox o usa sessione esistente.
4. Client mostra eventi: piano, comandi, file, tool call, richieste rete bloccate.
5. Azioni rischiose richiedono approve/deny.

Regola: comando operativo mai nascosto. Ogni operazione con impatto su file, rete, credenziali, deploy, acquisti, cancellazioni deve essere visibile e confermabile.

## UI desktop

Shell:

- `NavigationView` a sinistra: Chat, Agenti/Task, Server, Impostazioni.
- Area principale: lista conversazioni + thread messaggi + composer.
- Pannello destro opzionale su desktop largo: dettagli modello, task status, tool/event timeline.
- Breakpoint stretti: lista chat collassabile, pannello destro nascosto.

Componenti:

- `ListView` o virtualized list per conversazioni.
- `ItemsRepeater`/virtualized message list per thread lunghi.
- `TextBox` multilinea per composer, invio con `Ctrl+Enter` o `Enter` configurabile.
- `CommandBar` per azioni thread: nuovo, stop, export, search, settings.
- `InfoBar` per stato offline, auth scaduta, server non raggiungibile.
- `ContentDialog` per approve/deny task rischiosi.

Design:

- Fluent nativo, light/dark/high contrast.
- Mica o system backdrop nella shell, non nei contenuti che devono restare leggibili.
- Messaggi con larghezza massima, Markdown renderer, code blocks copiabili.
- Event timeline distinta dai messaggi: l'utente deve capire differenza tra testo AI e azione agente.

## Struttura progetto

```text
src/
  Nemoclaw.App.Windows/
    App.xaml
    MainWindow.xaml
    Views/
      ChatPage.xaml
      TasksPage.xaml
      ServerPage.xaml
      SettingsPage.xaml
    ViewModels/
    Services/
      NemoclawApiClient.cs
      StreamingClient.cs
      CredentialStore.cs
      LocalCache.cs
    Models/
    Controls/
    Resources/
tests/
  Nemoclaw.App.Windows.Tests/
docs/
```

Pattern:

- MVVM con CommunityToolkit.Mvvm se serve ridurre boilerplate.
- `HttpClient` con timeout, retry controllato, cancellation token.
- `IAsyncEnumerable<StreamEvent>` per streaming.
- SQLite locale solo per cache e bozze; server resta source of truth.
- Dependency injection leggera in `App.xaml.cs`.

## Modelli dati minimi

```text
Conversation:
  id, title, mode(chat|agent), model, createdAt, updatedAt

Message:
  id, conversationId, role(user|assistant|system|tool), content, status, createdAt

Task:
  id, conversationId, goal, status(queued|running|waiting_approval|done|failed|cancelled), createdAt

TaskEvent:
  id, taskId, type(plan|command|tool|network_request|file_change|approval|log|error), payload, createdAt

ServerStatus:
  online, version, nemoclawVersion, activeModel, sandboxHealth, inferenceHealth
```

## Sicurezza

Regole non negoziabili:

- Nessun token NemoClaw/Ollama diretto nel client.
- Nessuna password salvata in chiaro.
- Nessun HTTP non cifrato fuori da build debug locale.
- Token revocabili per dispositivo.
- Log locali senza prompt sensibili di default.
- Export chat protetto da conferma.
- Task agente con audit trail immutabile lato server.

Permessi:

- App Windows non deve avere accesso filesystem arbitrario salvo file picker scelto da utente.
- Allegati caricati al gateway con limite dimensione e scansione tipo file.
- Download generati salvati solo tramite picker o cartella configurata.

## Stato offline

Supportare:

- Server offline: UI read-only su cache conversazioni recenti.
- Draft locali persistenti.
- Retry invio messaggi quando connessione torna.
- Indicatore chiaro: `Offline`, `Connesso`, `Sandbox non pronta`, `Modello non raggiungibile`.

## Piano sviluppo

Fase 0: Specifica

- Definire API gateway OpenAPI.
- Definire modello eventi streaming.
- Definire policy sicurezza e pairing.

Fase 1: Skeleton Windows

- Creare app WinUI 3 packaged.
- Shell con NavigationView.
- Pagine placeholder.
- Settings endpoint server.

Fase 2: Chat base

- Login/pairing.
- Lista conversazioni.
- Invio messaggio.
- Streaming risposta.
- Stop/retry.

Fase 3: Task agente

- Creazione task.
- Timeline eventi.
- Approve/deny.
- Cancel.
- Server status live.

Fase 4: Polishing

- Markdown/code block.
- Search.
- Export.
- Cache SQLite.
- Notifiche Windows per task completati.

Fase 5: Hardening

- Test rete instabile.
- Token refresh/revoke.
- Certificato self-signed/VPN.
- Accessibility keyboard/Narrator.

## Test minimi

- Server non raggiungibile.
- Token scaduto.
- Streaming interrotto.
- Messaggio lungo.
- Conversazione con 10k messaggi.
- Task in attesa approvazione.
- Task cancellato durante tool call.
- Cambio modello.
- Riavvio app durante streaming/task.
- Tema light/dark/high contrast.

## Decisioni aperte

- Nome app.
- Gateway verra' creato in questo progetto o esiste gia'?
- NemoClaw gira su Linux, Windows+WSL o altra macchina?
- Accesso da Android fuori casa: VPN/Tailscale o solo LAN?
- Storico chat solo server o anche sync locale completo?

## Fonti

- Microsoft WinUI 3: https://learn.microsoft.com/windows/apps/winui/
- Microsoft Windows App SDK: https://learn.microsoft.com/windows/apps/windows-app-sdk/
- Microsoft WinUI setup: https://learn.microsoft.com/windows/apps/get-started/start-here
- Microsoft .NET MAUI platforms, valutato ma non scelto come default: https://learn.microsoft.com/dotnet/maui/supported-platforms
- NVIDIA NemoClaw local inference: https://docs.nvidia.com/nemoclaw/0.0.26/inference/use-local-inference.html
- NVIDIA NemoClaw architecture: https://docs.nvidia.com/nemoclaw/0.0.26/reference/architecture.html
- NVIDIA NemoClaw security best practices: https://docs.nvidia.com/nemoclaw/0.0.26/security/best-practices.html
- NVIDIA NemoClaw monitoring: https://docs.nvidia.com/nemoclaw/0.0.26/monitoring/monitor-sandbox-activity.html
