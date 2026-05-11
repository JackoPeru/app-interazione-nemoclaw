# Guida Windows Desktop Hermes Hub

## Obiettivo

App WinUI 3 per usare Hermes Agent dal desktop Windows con UI chatbot moderna.

Nome visibile: `Hermes Hub`.

Compatibilita storage mantenuta su `%LOCALAPPDATA%\ChatClaw` per non perdere cronologia e update chain.

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
- Hermes Visual Blocks v1 per spiegazioni visuali statiche sicure; `output_text` resta sempre fallback completo.

## Build

```powershell
dotnet build .\src\NemoclawChat.Windows\NemoclawChat.Windows.csproj -c Debug -p:Platform=x64
```
