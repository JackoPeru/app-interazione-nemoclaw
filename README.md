# ChatClaw

Client Windows + Android per parlare con un home-server OpenClaw e una IA locale.

## Progetti

- Windows: `src/NemoclawChat.Windows`
- Android: `src/NemoclawChat.Android`
- Guide: `docs/windows-desktop-guide.md`, `docs/android-app-guide.md`
- Preset: `config/nemoclaw-defaults.json`

## Stato attuale

- Windows WinUI 3: UI dark stile ChatGPT, sidebar, chat, archivio, ordini, server, settings, profilo e updater.
- Android Compose: UI mobile dark stile ChatGPT, composer, menu `+`, archivio, ordini, server, settings, profilo e updater in-app.
- Chat: prova prima il gateway OpenClaw reale su `/api/chat/stream`, poi `/api/chat`; se il gateway non risponde usa fallback locale solo quando abilitato.
- Ordini agente: task persistenti, tentativo reale su `/api/tasks`, approve/deny/complete sincronizzabili con fallback locale.
- Server: health check `/api/health` e snapshot `/api/server/status` quando disponibile.
- Update: Android scarica APK in app e apre installer; Windows scarica asset `.msix`, `.exe` o `.zip` e apre l'asset scaricato.

## Preset OpenClaw

Preset in [config/nemoclaw-defaults.json](config/nemoclaw-defaults.json):

- Gateway client: `https://openclaw.local:8443`
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

## Release

Ultima release pubblicata:

```text
v0.4.0
```

Asset attesi dagli updater:

- Android: `.apk`
- Windows: `.msix`, `.exe` o `.zip`

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

Il server reale resta il blocco principale prima dell'uso completo: senza gateway, le app sono pronte come client locale con fallback, archivio e updater.
