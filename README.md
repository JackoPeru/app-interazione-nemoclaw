# ChatClaw

Client Windows + Android per parlare con un home-server OpenClaw e una IA locale.

## Progetti

- Windows: `src/NemoclawChat.Windows`
- Android: `src/NemoclawChat.Android`
- Admin Bridge: `src/ChatClaw.AdminBridge`
- Guide: `docs/windows-desktop-guide.md`, `docs/android-app-guide.md`
- Preset: `config/nemoclaw-defaults.json`

## Stato attuale

- Windows WinUI 3: UI dark stile ChatGPT, sidebar, chat, archivio, ordini, server, settings, profilo e updater.
- Android Compose: UI mobile dark stile ChatGPT, composer, menu `+`, archivio, ordini, server, settings, profilo e updater in-app.
- Chat: prova prima il gateway OpenClaw reale su `/api/chat/stream`, poi `/api/chat`; se il gateway non risponde usa fallback locale solo quando abilitato.
- Ordini agente: task persistenti, tentativo reale su `/api/tasks`, approve/deny/complete sincronizzabili con fallback locale.
- Server: health check REST legacy, Gateway WebSocket OpenClaw, console operatore RPC, Admin Bridge opzionale.
- Console operatore: preset RPC reali per modelli, plugin, approvals, config, workspace, log, canali, cron, nodi, security, memoria, secrets e update.
- Admin Bridge: servizio .NET con auth bearer, allowlist comandi, root filesystem consentite, backup file e audit log.
- Update: Android scarica APK in app e apre installer; Windows scarica asset `.msix`, `.exe` o `.zip` e apre l'asset scaricato.

## Preset OpenClaw

Preset in [config/nemoclaw-defaults.json](config/nemoclaw-defaults.json):

- Gateway client: `https://openclaw.local:8443`
- Gateway WS: `wss://openclaw.local:8443`
- Admin Bridge: `https://openclaw.local:9443`
- Provider lato server: `custom`
- Endpoint inferenza lato server: `http://localhost:8000/v1`
- API default OpenAI-compatible: `/v1/chat/completions`
- Modello default: `meta-llama/Llama-3.1-8B-Instruct`
- Accesso consigliato: `VPN/LAN only`

## Build

Windows:

```powershell
dotnet build .\src\NemoclawChat.Windows\NemoclawChat.Windows.csproj -c Debug -p:Platform=x64
```

Android:

```powershell
$env:ANDROID_HOME='C:\Users\Matteo\AppData\Local\Android\Sdk'
$env:ANDROID_SDK_ROOT=$env:ANDROID_HOME
cd .\src\NemoclawChat.Android
.\gradlew.bat assembleDebug
```

APK debug:

```text
src/NemoclawChat.Android/app/build/outputs/apk/debug/app-debug.apk
```

Admin Bridge:

```powershell
dotnet build .\src\ChatClaw.AdminBridge\ChatClaw.AdminBridge.csproj -c Debug
```

## Release

Ultima release pubblicata:

```text
v0.5.2
```

Asset attesi dagli updater:

- Android: `.apk`
- Windows: `.msix`, `.exe` o `.zip`
- Admin Bridge: incluso nel sorgente, deploy manuale sul server.

## Gateway OpenClaw

Contratto supportato dal client:

- `GET /api/health`
- `GET /api/server/status`
- `POST /api/chat/stream`
- `POST /api/chat`
- `POST /api/tasks`
- `POST /api/tasks/{id}/approve`
- `POST /api/tasks/{id}/deny`
- `POST /api/tasks/{id}/complete`

Il server reale resta necessario per validare runtime RPC: senza OpenClaw Gateway acceso, la Console mostra errori reali di connessione/metodo non supportato.
