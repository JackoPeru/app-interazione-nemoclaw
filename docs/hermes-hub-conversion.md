# Hermes Hub Conversion

## Scope

ChatClaw e' stato convertito in `Hermes Hub` come client operativo per Hermes Agent.

Compatibilita mantenuta:

- Android `applicationId`: `com.nemoclaw.chat`.
- Storage locale: directory/preferenze `ChatClaw` mantenute per non perdere archivio e update chain.
- Logo esistente mantenuto temporaneamente in assenza di asset Hermes.

## Hermes API

Default:

```text
Hermes API URL: http://hermes.local:8642/v1
Health root: http://hermes.local:8642/health
Model: hermes-agent
Auth: Authorization: Bearer <Hermes API key>
```

Chat:

- Primario: `POST /v1/responses`.
- Payload: `model`, `input`, `instructions`, `store: true`, `conversation`, `previous_response_id`.
- Fallback: `POST /v1/chat/completions`.
- Output parser: `output_text`, `message.content`, `choices[].message.content`, testo SSE.

Dashboard:

- `GET /health`.
- `GET /health/detailed`.
- `GET /v1/models`.
- `GET /v1/capabilities`.

Runs:

- `POST /v1/runs`.
- Endpoint manuale supporta anche `GET /v1/runs/{run_id}`, `GET /v1/runs/{run_id}/events`, `POST /v1/runs/{run_id}/stop`.

Jobs:

- `GET /api/jobs`.
- `POST /api/jobs`.
- `POST /api/jobs/{job_id}/run`.
- `POST /api/jobs/{job_id}/pause`.
- `DELETE /api/jobs/{job_id}`.

## Notes

Admin Bridge resta nel repo come tool legacy/dev, ma non e' requisito primario Hermes e non e' esposto nella UX principale Android/Windows.
