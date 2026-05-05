# AGENTS.md

## Obiettivo

Creare app chat stile ChatGPT per comunicare con home-server che ospitera' OpenClaw e una IA locale.

Target:

- App desktop Windows.
- App Android.
- UI moderna chatbot: dark premium operator console, composer largo, menu `+`, modalita `Chat`/`Agente`.
- App name: `ChatClaw`.
- Gateway comune futuro: `OpenClaw Gateway API`.
- Direzione nuova: ChatClaw deve diventare console operatore OpenClaw, non solo frontend REST.

## Regola Git

Non fare commit o push su GitHub a meno che Matteo lo chieda esplicitamente.

Repo remoto:

```text
https://github.com/JackoPeru/app-interazione-nemoclaw.git
```

Branch usato:

```text
main
```

Ultimo push fatto su richiesta utente:

```text
1b4997f Release ChatClaw 0.5.0
```

## Regola Memoria

Aggiornare questo file ogni volta che cambia qualcosa di importante nel progetto:

- obiettivo o scope;
- stato Windows/Android;
- decisioni tecniche;
- impostazioni default;
- comandi build/test;
- regole Git;
- bug noti;
- TODO/prossimi passi;
- preferenze utente.

Non lasciare `AGENTS.md` obsoleto dopo modifiche rilevanti.

## Stato Attuale

Windows:

- Progetto: `src/NemoclawChat.Windows`
- Stack: WinUI 3, C#, .NET 8, Windows App SDK self-contained.
- Versione app: `0.5.1`.
- Brand/UI: `ChatClaw`, logo nuovo applicato agli asset Windows e alla UI principale, dark stile ChatGPT, sidebar, composer largo, menu `+`, settings reali.
- UI design system applicato: superfici elevation-aware `#0F1115/#14171D/#1A1E26/#232831`, accent claw blue `#2DA8FF`, testo muted `#A2ADBF`, bubble utente blu `#1F4FA8`, card/composer radius premium e bordi soft.
- Azioni locali: file picker Windows, screen clip, camera URI, nota vocale prompt.
- Chat: invio con Enter, nuova riga con Shift+Enter, action bubble per menu `+`, scroll automatico, salvataggio cronologia locale.
- Archivio: ricerca locale + dati persistenti, filtri chat/progetti/task/server, riapertura conversazioni, segna progetto.
- Recenti sidebar: letti dallo store locale e aggiornati quando cambia archivio; nessun elemento seed finto.
- Chat: prova prima il gateway reale (`/api/chat/stream`, fallback `/api/chat`), poi usa fallback locale solo se abilitato nelle impostazioni.
- Ordini agente: coda task persistente su disco, tentativo reale su gateway (`/api/tasks`), approve/deny/completa con sync gateway se disponibile e fallback locale se no.
- Server: dashboard gateway/modello/inferenza/sicurezza, test `/api/health`, lettura reale di `/api/server/status` quando disponibile.
- Gateway OpenClaw WS: pagina Server include test WebSocket ufficiale, handshake RPC `connect` con `minProtocol/maxProtocol=3`, ruolo `operator`, auth token nel frame, probe metodi `status`, `system-presence`, `models.status`, `models.list`, `plugins.list`, `channels.status`, `nodes.list`, `exec.approvals.get`.
- Console Operatore: pagina dedicata con RPC manuale e preset reali per dashboard, modelli, plugin, approvals, config, log, canali, cron, nodi, security, memoria, secrets e update. Ogni azione chiama Gateway WS; metodo non supportato viene mostrato come errore reale.
- Console Fase 2/3: controlli dedicati per `exec.approval.resolve`, `config.get/patch/apply`, `workspace.files.list/read/write`, Admin Bridge status/doctor/security audit/restart/tail log.
- Settings: aggiunto `GatewayWsUrl`, default `wss://openclaw.local:8443`.
- Settings: aggiunto `AdminBridgeUrl`, default `https://openclaw.local:9443`.
- Credenziale Gateway: token/password salvato in Windows Credential Locker; campo vuoto mantiene segreto esistente, reset lo elimina.
- Profilo/About: info app/profilo locale, versione, privacy, gateway attivo.
- Update system: controlla GitHub Releases latest, scarica asset Windows in app con progresso e poi apre installer/asset da bottone `Installa update`.
- Compatibilita storage: usa `%LOCALAPPDATA%\\ChatClaw\\...` ma migra automaticamente da `%LOCALAPPDATA%\\NemoclawChat\\...` se esiste.
- Settings: validazione URL/campi obbligatori, salvataggio locale, reset default, test gateway `/api/health`.
- Settings salvate in:

```text
%LOCALAPPDATA%\ChatClaw\settings.json
```

Conversazioni/progetti salvati in:

```text
%LOCALAPPDATA%\ChatClaw\conversations.json
```

Android:

- Progetto: `src/NemoclawChat.Android/app`
- Stack: Kotlin, Jetpack Compose, Gradle.
- Versione app: `0.5.1`, versionCode `9`.
- Brand/UI: `ChatClaw`, logo nuovo applicato a launcher + UI, bottom nav con icone vere, composer mobile rifatto, menu `+` con Material icons, profilo locale.
- UI design system applicato: superfici elevation-aware `#0F1115/#14171D/#1A1E26/#232831`, accent claw blue `#2DA8FF`, testo muted `#A2ADBF`, bubble utente blu `#1F4FA8`, empty state con wash blu e logo grande.
- Azioni locali: file picker Android, camera intent, dettatura intent, fallback testuale se intent non disponibile.
- Chat: action bubble per menu `+`, mode `Chat`/`Agente`, tentativo gateway reale (`/api/chat/stream`, fallback `/api/chat`), fallback locale esplicito se abilitato, composer stabile a campo singolo, chip mode in alto a destra, salvataggio cronologia locale.
- Archivio: tab mobile con ricerca locale persistente, filtri, riapertura conversazioni, salvataggio progetti, contatori, export appunti, rename/delete conversazioni salvate; nessun seed progetto/chat finto.
- Ordini agente: coda task persistente in `SharedPreferences`, creazione task con tentativo reale su gateway, approve/deny/completa con sync gateway se disponibile e fallback locale se no.
- Server: dashboard gateway/modello/inferenza/sicurezza, test `/api/health`, lettura reale di `/api/server/status` quando disponibile.
- Gateway OpenClaw WS: pagina Server include test WebSocket ufficiale, handshake RPC `connect` con `minProtocol/maxProtocol=3`, ruolo `operator`, auth token nel frame, probe metodi `status`, `system-presence`, `models.status`, `models.list`, `plugins.list`, `channels.status`, `nodes.list`, `exec.approvals.get`.
- Console Operatore: tab dedicata con RPC manuale e preset reali per dashboard, modelli, plugin, approvals, config, log, canali, cron, nodi, security, memoria, secrets e update. Ogni azione chiama Gateway WS; metodo non supportato viene mostrato come errore reale.
- Console Fase 2/3: controlli dedicati per `exec.approval.resolve`, `config.get/patch/apply`, `workspace.files.list/read/write`, Admin Bridge status/doctor/security audit/restart/tail log.
- Settings: aggiunto `gatewayWsUrl`, default `wss://openclaw.local:8443`.
- Settings: aggiunto `adminBridgeUrl`, default `https://openclaw.local:9443`.
- Credenziale Gateway: token/password cifrato con Android Keystore AES-GCM e salvato in `chatclaw_settings`; campo vuoto mantiene segreto esistente, reset lo elimina.
- Profilo: info Matteo/app/gateway/privacy/parita Windows.
- Update system: controlla GitHub Releases latest, scarica APK dentro l'app con progress bar + dimensione file e poi apre installer Android con tasto `Aggiorna`.
- Nessun bottone `Release` nella UI update Android: il flusso resta interno all'app come UniNote (`Controlla > Scarica > Aggiorna`).
- Se la versione installata e' gia' l'ultima disponibile: mostra solo stato aggiornato e il controllo refresh, senza bottoni `Scarica`/`Aggiorna`.
- Top bar chat Android: niente label `Demo: ...`; mostra solo brand + chip `Chat/Agente`.
- Icona launcher Android: adaptive icon con foreground ritagliato piu' grande per ridurre il vuoto attorno al logo tra le app.
- Settings: validazione URL/campi obbligatori, salvataggio locale, reset default, test gateway `/api/health`.
- Settings salvate in `SharedPreferences` `chatclaw_settings` con migrazione automatica da `nemoclaw_settings`.
- Conversazioni/progetti salvati in `SharedPreferences` `chatclaw_archive` con migrazione automatica da `nemoclaw_archive`.
- Task salvati in `SharedPreferences` `chatclaw_tasks`.
- Progetto Android ora include file root Gradle + wrapper per essere buildabile dal repo.

Documentazione:

- `docs/windows-desktop-guide.md`
- `docs/android-app-guide.md`
- `docs/openclaw-operator-console-implementation.md`
- `config/nemoclaw-defaults.json`

Admin Bridge:

- Progetto: `src/ChatClaw.AdminBridge`
- Stack: ASP.NET Core minimal API, .NET 8.
- Auth: bearer token da `CHATCLAW_ADMIN_TOKEN`.
- File root: `CHATCLAW_ADMIN_ROOTS`.
- Audit log: `~/.chatclaw-admin-bridge/audit.log` o `CHATCLAW_ADMIN_AUDIT`.
- Endpoints reali: `/v1/status`, `/v1/actions/{doctor|security-audit|update-openclaw|plugin-list|restart-gateway}`, `/v1/logs/tail`, `/v1/files/list`, `/v1/files/read`, `/v1/files/write`.

## Preset OpenClaw

Usare questi default finche' server reale non esiste:

```text
Gateway URL: https://openclaw.local:8443
Gateway WS URL: wss://openclaw.local:8443
Gateway WS protocol: 3
Gateway WS role/scopes: operator / operator.read, operator.approvals, operator.config, operator.logs
Provider: custom
Endpoint inferenza lato server: http://localhost:8000/v1
API: /v1/chat/completions
Model demo: meta-llama/Llama-3.1-8B-Instruct
Accesso: VPN/LAN only
```

Nota architetturale:

- App non devono parlare direttamente con NemoClaw/Ollama/local inference.
- App devono parlare al futuro `OpenClaw Gateway API`.
- Control plane primario: OpenClaw Gateway WebSocket ufficiale. REST `/api/*` resta fallback legacy fino a backend reale.
- Segreti/provider token restano lato server.

## UI Composer

Direzione richiesta utente:

- Composer deve essere largo come ChatGPT desktop, non stretto.
- Prompt deve occupare la maggior parte dello spazio.
- Finche' testo e' corto: una riga ampia.
- Quando testo va a fine riga: composer cresce in altezza mantenendo larghezza.
- Scelta `Chat`/`Agente` deve stare dentro menu `+`, non occupare spazio principale.
- Menu `+` deve avere simboli/icone per capire subito funzioni.
- Android: bottom nav deve usare icone vere, non lettere placeholder.

## Build

Windows:

```powershell
dotnet build .\src\NemoclawChat.Windows\NemoclawChat.Windows.csproj -c Debug -p:Platform=x64
```

Admin Bridge:

```powershell
dotnet build .\src\ChatClaw.AdminBridge\ChatClaw.AdminBridge.csproj -c Debug
```

Android:

```powershell
$env:ANDROID_HOME='C:\Users\Matteo\AppData\Local\Android\Sdk'
$env:ANDROID_SDK_ROOT=$env:ANDROID_HOME
.\gradlew.bat assembleDebug
```

APK debug:

```text
src/NemoclawChat.Android/app/build/outputs/apk/debug/app-debug.apk
```

Nota update:

- Le app controllano `https://api.github.com/repos/JackoPeru/app-interazione-nemoclaw/releases/latest`.
- Tag release atteso: `vX.Y.Z`, esempio `v0.3.2`.
- Android richiede APK con stesso `applicationId` e stessa firma, `versionCode` maggiore.
- Android updater ora supporta download in-app con barra progresso e poi handoff all'installer di sistema.
- Windows ora scarica asset release `.msix`, `.exe` o `.zip` dentro `%LOCALAPPDATA%\ChatClaw\updates\` e poi apre l'installer/asset.

Android SDK locale:

```text
.android-sdk/
```

Questo e' ignorato da Git.

## File Ignorati

Non aggiungere a Git:

```text
.android-sdk/
.tools/
.gradle/
local.properties
bin/
obj/
src/NemoclawChat.Android/app/build/
```

## Prossimi Passi Probabili

- Migliorare ulteriormente UI desktop/mobile.
- Migliorare UI verticale oltre console unica: pagine separate per approvals realtime, config diff/rollback visuale, file/workspace manager, Admin Bridge, security center avanzato.
- Se arriva backend definitivo, allineare i payload/contratti reali se differiscono da quelli flessibili attuali.
- Prossima milestone: trasformare i probe WS in pagine operative vere: approvazioni exec realtime, modelli/provider, plugin/skills, log/audit, nodi, workspace, config patch, security center.
- Migliorare pairing completo: QR/pairing device e revoca dispositivi. Base credenziali sicure gia' presente: Windows Credential Locker, Android Keystore.
- Collegare evento `exec.approval.requested` e RPC `exec.approval.resolve` quando Gateway reale disponibile.
- Eventuale rebrand tecnico completo dei namespace interni solo se non rompe la compatibilita update Android.
- Aggiungere import archivio locale se richiesto.

## Preferenze Comunicazione

Utente ha attivato `caveman`: risposte brevi, tecniche, senza filler.

Usare italiano.
