# AGENTS.md

## Obiettivo

Creare app chat stile ChatGPT per comunicare con home-server che ospita Hermes Agent.

Target:

- App desktop Windows.
- App Android.
- UI moderna chatbot: dark premium operator console, composer largo, menu `+`, modalita `Chat`/`Agente`.
- Nome visibile app: `Hermes Hub`.
- Compatibilita Android: `applicationId` resta `com.nemoclaw.chat`.
- Backend primario: Hermes Agent API Server su `http://hermes.local:8642/v1`.
- Direzione nuova: Hermes Hub e' client operativo per Hermes Agent, non console del vecchio backend.

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

Ultimo push release fatto su richiesta utente:

```text
0c28b24 Release Hermes Hub 0.6.3
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
- Versione app: `0.6.3`.
- Brand/UI: `Hermes Hub`, logo Hermes da `logo hermeshub.png` applicato agli asset Windows e alla UI principale, dark stile ChatGPT, sidebar, composer largo, menu `+`, settings reali.
- UI design system applicato: superfici elevation-aware `#0F1115/#14171D/#1A1E26/#232831`, accent Hermes amber `#F5A524`, hover `#FFC857`, testo muted `#A2ADBF`, bubble utente amber scuro `#7A3E00`, card/composer radius premium e bordi soft.
- Azioni locali: file picker Windows, screen clip, camera URI, nota vocale prompt.
- Chat: invio con Enter, nuova riga con Shift+Enter, chip `Chat`/`Agente` in alto a destra cliccabile, action bubble per menu `+`, scroll automatico, salvataggio cronologia locale.
- Archivio: ricerca locale + dati persistenti, filtri chat/progetti/task/server, riapertura conversazioni, segna progetto, eliminazione elementi salvati con conferma preventiva.
- Recenti sidebar: letti dallo store locale e aggiornati quando cambia archivio; nessun elemento seed finto.
- Chat: prova prima Hermes `POST /v1/responses` con `store`, `conversation` e `previous_response_id`; fallback reale `POST /v1/chat/completions`; fallback locale solo se abilitato.
- Visual Blocks v1 implementato lato client: chat puo' ricevere `output_text` autosufficiente + `visual_blocks_version: 1` + blocchi tipizzati statici (`markdown`, `code`, `table`, `chart`, `diagram`, `image_gallery`, `callout`). Contratto in `docs/visual-blocks-schema.md` e schema in `config/visual-blocks.schema.json`.
- Jobs: coda persistente su disco, tentativo reale su Hermes Jobs API (`/api/jobs`), `run`/`pause`/`delete` con sync Hermes se disponibile e fallback locale se no.
- Server: dashboard Hermes con `/health`, `/health/detailed`, `/v1/models`, `/v1/capabilities`.
- Runs: pagina dedicata con preset HTTP reali per health, models, capabilities, `POST /v1/runs`, `GET/POST /api/jobs`.
- Vecchio WebSocket operator rimosso dalla UX principale. Servizi legacy restano nel repo non primari.
- Settings: `GatewayUrl` ora significa `Hermes API URL`, default `http://hermes.local:8642/v1`; `GatewayWsUrl` vuoto; `AdminBridgeUrl` derivato root Hermes.
- Credenziale Hermes API key: salvata in Windows Credential Locker; campo vuoto mantiene segreto esistente, reset lo elimina.
- Profilo/About: info app/profilo locale, versione, privacy, gateway attivo.
- Update system: controlla GitHub Releases latest, scarica asset Windows in app con progresso e poi apre installer/asset da bottone `Installa update`.
- Compatibilita storage: usa `%LOCALAPPDATA%\\ChatClaw\\...` ma migra automaticamente da `%LOCALAPPDATA%\\NemoclawChat\\...` se esiste.
- Settings: validazione URL/campi obbligatori, salvataggio locale, reset default, test Hermes `/health`.
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
- Versione app: `0.6.3`, versionCode `16`.
- Brand/UI: `Hermes Hub`, logo Hermes da `logo hermeshub.png` applicato a launcher + UI, bottom nav con icone vere, composer mobile rifatto, menu `+` con Material icons, profilo locale.
- UI design system applicato: superfici elevation-aware `#0F1115/#14171D/#1A1E26/#232831`, accent Hermes amber `#F5A524`, testo muted `#A2ADBF`, bubble utente amber scuro `#7A3E00`, empty state con wash amber e logo grande.
- Azioni locali: file picker Android, camera intent, dettatura intent, fallback testuale se intent non disponibile.
- Chat: action bubble per menu `+`, mode `Chat`/`Agente`, chip mode in alto a destra cliccabile, tentativo Hermes reale (`/v1/responses`, fallback `/v1/chat/completions`), fallback locale esplicito se abilitato, composer stabile a campo singolo, salvataggio cronologia locale con `previous_response_id`.
- Visual Blocks v1 implementato lato client: stesso contratto Windows, storage retrocompatibile, renderer Compose statico sicuro, nessun HTML/JS/SVG client-side.
- Archivio: tab mobile con ricerca locale persistente, filtri, riapertura conversazioni, salvataggio progetti, contatori, export appunti, rename/delete conversazioni salvate, conferma preventiva prima del delete, icona delete sempre visibile sulla card e azioni che vanno a capo su schermi stretti; nessun seed progetto/chat finto.
- Android UI hardening: righe di azioni in Archivio, Ordini, Server, Aggiornamenti e Settings convertite a layout che va a capo su schermi stretti, per evitare pulsanti nascosti o compressi.
- Jobs: coda task persistente in `SharedPreferences`, creazione job con tentativo reale su Hermes Jobs API, run/pause/delete con sync Hermes se disponibile e fallback locale se no.
- Server: dashboard Hermes/modello/API/sicurezza, test `/health`, lettura reale di `/health/detailed`, `/v1/models`, `/v1/capabilities`.
- Runs: tab dedicata con endpoint manuale e preset reali Hermes per dashboard, modelli, capabilities, runs e jobs. Vecchio WS operator rimosso dalla UX principale.
- Settings: `gatewayWsUrl` vuoto, non mostrato nella UX Hermes.
- Settings: `adminBridgeUrl` derivato da root Hermes, non requisito primario.
- Credenziale Hermes API key: cifrata con Android Keystore AES-GCM e salvata in `chatclaw_settings`; campo vuoto mantiene segreto esistente, reset lo elimina.
- Profilo: info Matteo/app/gateway/privacy/parita Windows.
- Update system: controlla GitHub Releases latest, scarica APK dentro l'app con progress bar + dimensione file e poi apre installer Android con tasto `Aggiorna`.
- Nessun bottone `Release` nella UI update Android: il flusso resta interno all'app come UniNote (`Controlla > Scarica > Aggiorna`).
- Se la versione installata e' gia' l'ultima disponibile: mostra solo stato aggiornato e il controllo refresh, senza bottoni `Scarica`/`Aggiorna`.
- Top bar chat Android: niente label `Demo: ...`; mostra solo brand + chip `Chat/Agente`.
- Icona launcher Android: adaptive icon con foreground ritagliato piu' grande per ridurre il vuoto attorno al logo tra le app.
- Settings: validazione URL/campi obbligatori, salvataggio locale, reset default, test Hermes `/health`.
- Settings salvate in `SharedPreferences` `chatclaw_settings` con migrazione automatica da `nemoclaw_settings`.
- Conversazioni/progetti salvati in `SharedPreferences` `chatclaw_archive` con migrazione automatica da `nemoclaw_archive`.
- Task salvati in `SharedPreferences` `chatclaw_tasks`.
- Progetto Android ora include file root Gradle + wrapper per essere buildabile dal repo.

Documentazione:

- `docs/windows-desktop-guide.md`
- `docs/android-app-guide.md`
- `docs/hermes-hub-conversion.md`
- `docs/visual-blocks-schema.md`
- `config/hermes-defaults.json`
- `config/visual-blocks.schema.json`

Admin Bridge:

- Progetto: `src/ChatClaw.AdminBridge`
- Stack: ASP.NET Core minimal API, .NET 8.
- Auth: bearer token da `CHATCLAW_ADMIN_TOKEN`.
- File root: `CHATCLAW_ADMIN_ROOTS`.
- Audit log: `~/.chatclaw-admin-bridge/audit.log` o `CHATCLAW_ADMIN_AUDIT`.
- Endpoints reali: `/v1/status`, `/v1/actions/{doctor|security-audit|plugin-list}`, `/v1/logs/tail`, `/v1/files/list`, `/v1/files/read`, `/v1/files/write`.

## Preset Hermes

Usare questi default finche' server reale non esiste:

```text
Hermes API URL: http://hermes.local:8642/v1
Health root: http://hermes.local:8642/health
Detailed health: http://hermes.local:8642/health/detailed
Provider: hermes-agent
Endpoint API lato server: http://hermes.local:8642/v1
API primaria: POST /v1/responses
API fallback: POST /v1/chat/completions
Model demo: hermes-agent
Accesso: Tailscale/LAN
Auth: Authorization: Bearer <Hermes API key>
```

Nota architetturale:

- App non devono parlare direttamente con Ollama/local inference.
- App devono parlare a Hermes Agent API Server.
- Chat primaria via Responses API con `store`, `conversation` e `previous_response_id`.
- Runs API e Jobs API sostituiscono la vecchia console operator WS.
- Segreti/provider token restano lato server; la Hermes API key e' l'unico segreto client.
- Visual Blocks v1: `output_text` deve essere completo anche quando ci sono blocchi; history inviata a Hermes deve contenere solo testo umano, non JSON dei blocchi. Client dichiara `metadata.visual_blocks.min_supported_version/max_supported_version/mode`. `mode`: `never` disabilita, `auto` lascia decidere Hermes, `always` preferisce blocchi quando ragionevole senza forzarli.
- Sicurezza Visual Blocks: niente HTML, JS o SVG client-side. Diagrammi solo Mermaid source + media proxy pre-renderizzato con fallback code block `mermaid`. Media solo da proxy Hermes, no `file://`, no `data:`, no URL esterni diretti.
- Quicktype Visual Blocks: dopo generazione tipi, controllare discriminator `type` ed enum wire-value C#/Kotlin con `scripts/verify-visual-blocks-contract.ps1`.
- Golden screenshot Visual Blocks: baseline unica versionata in `tests/golden/`, generata da Windows CI; Android confronta con tolleranza e non crea baseline autonoma.

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
- Android debug build non deve usare `applicationIdSuffix`: l'APK generato per release in-app deve restare `com.nemoclaw.chat`.
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

- Backlog idee app mantenuto in `prossime implementazioni.md`; idee iniziali: sezione video e sezione news.
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
