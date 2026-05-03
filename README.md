# Nemoclaw Chat

Client Windows + Android per home-server NemoClaw/local AI.

## Progetti

- Windows: `src/NemoclawChat.Windows`
- Android: `src/NemoclawChat.Android/app`
- Guide: `docs/windows-desktop-guide.md`, `docs/android-app-guide.md`

## Stato attuale

- Windows WinUI 3: UI dark stile ChatGPT, sidebar, chat mock, pagine ordini/server/settings.
- Android Compose: UI mobile dark stile ChatGPT, composer, messaggi mock, tab ordini/server/settings.
- Gateway reale non collegato ancora: UI usa preset demo finche' non esiste `Nemoclaw Gateway API`.

## Preset NemoClaw

Preset in [config/nemoclaw-defaults.json](config/nemoclaw-defaults.json):

- Gateway client: `https://nemoclaw.local:8443`
- Provider NemoClaw lato server: `custom`
- Endpoint inferenza lato server: `http://localhost:8000/v1`
- API default NemoClaw per endpoint OpenAI-compatible: `/v1/chat/completions`
- API key demo per endpoint locale senza auth: `dummy`

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
src/NemoclawChat.Android/app/build/outputs/apk/debug/androidApp-debug.apk
```

## Prossimo passo

Creare o collegare `Nemoclaw Gateway API`, poi sostituire i mock con:

- `GET /api/health`
- `GET /api/models`
- `POST /api/conversations/{conversationId}/messages`
- streaming SSE da `/api/conversations/{conversationId}/stream`
- task agentici da `/api/tasks`
