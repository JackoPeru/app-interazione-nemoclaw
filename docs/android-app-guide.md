# Guida Android Hermes Hub

## Obiettivo

App Android Jetpack Compose per usare Hermes Agent dal telefono con UI chatbot moderna.

Nome visibile: `Hermes Hub`.

Compatibilita aggiornamento preservata:

- `applicationId = com.nemoclaw.chat`.
- `versionCode` aumenta a ogni release.
- SharedPreferences `chatclaw_*` restano per non perdere dati.

## Backend

Default:

```text
Hermes API URL: http://hermes.local:8642/v1
Health: http://hermes.local:8642/health
Model: hermes-agent
Auth: Authorization: Bearer <Hermes API key>
```

Endpoint usati:

- `POST /v1/responses` per chat primaria.
- `POST /v1/chat/completions` come fallback.
- `GET /health`, `GET /health/detailed`, `GET /v1/models`, `GET /v1/capabilities` per dashboard.
- `POST /v1/runs` e endpoint manuali `/v1/runs/{run_id}` per Runs.
- `GET/POST/DELETE /api/jobs` e azioni `/run`, `/pause` per Jobs.

## Build

```powershell
$env:ANDROID_HOME='C:\Users\Matteo\AppData\Local\Android\Sdk'
$env:ANDROID_SDK_ROOT=$env:ANDROID_HOME
.\gradlew.bat assembleDebug
```

APK:

```text
src/NemoclawChat.Android/app/build/outputs/apk/debug/app-debug.apk
```
