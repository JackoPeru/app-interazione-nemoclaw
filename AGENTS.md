# AGENTS.md

## Obiettivo

Creare app chat stile ChatGPT per comunicare con home-server che ospitera' NemoClaw e una IA locale.

Target:

- App desktop Windows.
- App Android.
- UI moderna chatbot: dark, composer largo, menu `+`, modalita `Chat`/`Agente`.
- Gateway comune futuro: `Nemoclaw Gateway API`.

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
78386bf Add local chat archive persistence
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
- Versione app: `0.2.1`.
- UI: dark stile ChatGPT, sidebar, composer largo, menu `+`, settings reali.
- Azioni locali: file picker Windows, screen clip, camera URI, nota vocale prompt.
- Chat: invio con Enter, nuova riga con Shift+Enter, action bubble per menu `+`, scroll automatico, salvataggio cronologia locale.
- Archivio: ricerca locale + dati persistenti, filtri chat/progetti/task/server, riapertura conversazioni, segna progetto.
- Recenti sidebar: letti dallo store locale e aggiornati quando cambia archivio.
- Ordini agente: coda task locale, creazione task, template workspace/server, approve/deny/completa.
- Server: dashboard gateway/modello/inferenza/sicurezza, test `/api/health`, contratto API atteso.
- Profilo/About: info app/profilo locale, versione, privacy, gateway attivo.
- Update checker: controlla GitHub Releases latest, confronta versione locale, apre release/asset Windows.
- Settings: validazione URL/campi obbligatori, salvataggio locale, reset default, test gateway `/api/health`.
- Settings salvate in:

```text
%LOCALAPPDATA%\NemoclawChat\settings.json
```

Conversazioni/progetti salvati in:

```text
%LOCALAPPDATA%\NemoclawChat\conversations.json
```

Android:

- Progetto: `src/NemoclawChat.Android/app`
- Stack: Kotlin, Jetpack Compose, Gradle.
- Versione app: `0.2.1`, versionCode `3`.
- UI: dark stile ChatGPT mobile, composer largo, menu `+`, settings reali, profilo locale.
- Azioni locali: file picker Android, camera intent, dettatura intent, fallback testuale se intent non disponibile.
- Chat: action bubble per menu `+`, mode `Chat`/`Agente`, mock reply differenziata, menu ASCII senza mojibake, salvataggio cronologia locale.
- Archivio: tab mobile con ricerca locale persistente, filtri, riapertura conversazioni, salvataggio progetti, contatori, export appunti, rename/delete conversazioni salvate.
- Ordini agente: coda task locale, creazione task, template, approve/deny/completa.
- Server: dashboard gateway/modello/inferenza/sicurezza, test `/api/health`, contratto API atteso.
- Profilo: info Matteo/app/gateway/privacy/parita Windows.
- Update checker: controlla GitHub Releases latest, confronta versione locale, apre release/asset APK.
- Settings: validazione URL/campi obbligatori, salvataggio locale, reset default, test gateway `/api/health`.
- Settings salvate in `SharedPreferences` `nemoclaw_settings`.
- Conversazioni/progetti salvati in `SharedPreferences` `nemoclaw_archive`.

Documentazione:

- `docs/windows-desktop-guide.md`
- `docs/android-app-guide.md`
- `config/nemoclaw-defaults.json`

## Preset NemoClaw

Usare questi default finche' server reale non esiste:

```text
Gateway URL: https://nemoclaw.local:8443
Provider: custom
Endpoint inferenza lato server: http://localhost:8000/v1
API: /v1/chat/completions
Model demo: meta-llama/Llama-3.1-8B-Instruct
Accesso: VPN/LAN only
```

Nota architetturale:

- App non devono parlare direttamente con NemoClaw/Ollama/local inference.
- App devono parlare al futuro `Nemoclaw Gateway API`.
- Segreti/provider token restano lato server.

## UI Composer

Direzione richiesta utente:

- Composer deve essere largo come ChatGPT desktop, non stretto.
- Prompt deve occupare la maggior parte dello spazio.
- Finche' testo e' corto: una riga ampia.
- Quando testo va a fine riga: composer cresce in altezza mantenendo larghezza.
- Scelta `Chat`/`Agente` deve stare dentro menu `+`, non occupare spazio principale.
- Menu `+` deve avere simboli/icone per capire subito funzioni.

Azioni menu `+` attuali:

```text
[file] Aggiungi file al task
[shot] Cattura screenshot
[cam] Scatta foto
[chat] Modalita: Chat
[agent] Modalita: Agente
[image] Crea immagine
[research] Deep Research locale
[web] Ricerca web autorizzata
[project] Progetti / workspace
```

## Build

Windows:

```powershell
dotnet build .\src\NemoclawChat.Windows\NemoclawChat.Windows.csproj -c Debug -p:Platform=x64
```

Android:

```powershell
.\gradlew.bat :androidApp:assembleDebug
```

APK debug:

```text
src/NemoclawChat.Android/app/build/outputs/apk/debug/app-debug.apk
```

Nota update:

- Le app controllano `https://api.github.com/repos/JackoPeru/app-interazione-nemoclaw/releases/latest`.
- Tag release atteso: `vX.Y.Z`, esempio `v0.2.1`.
- Android richiede APK con stesso `applicationId` e stessa firma, `versionCode` maggiore.
- Windows richiede asset release `.msix`, `.exe` o `.zip`; installazione automatica completa richiede installer/updater dedicato.

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
- Implementare allegati veri per menu `+`.
- Implementare acquisizione screenshot/foto.
- Creare `Nemoclaw Gateway API`.
- Collegare streaming chat reale.
- Collegare task agente con approve/deny.
- Sostituire coda task demo con gateway reale.
- Aggiungere import archivio locale se richiesto.

## Preferenze Comunicazione

Utente ha attivato `caveman`: risposte brevi, tecniche, senza filler.

Usare italiano.
