# Hermes Gateway su Ubuntu/Linux

`hermes-hub` e' il launcher del **Hermes Gateway**: il servizio ponte/API server che espone Hermes Agent alle app Hermes Hub Windows/Android. Durante i test puo' puntare a LM Studio; sul server finale Ubuntu headless deve restare compatibile con vLLM.

Avvio manuale:

```bash
chmod +x scripts/hermes-hub-linux.sh
./scripts/hermes-hub-linux.sh
```

Default:

```text
Hermes Gateway API: http://0.0.0.0:8642/v1
API key: hermes-hub
Provider: lm_studio
Inference: http://127.0.0.1:1234/v1
Config: ~/.hermes/config.yaml
Env: ~/.hermes/.env
```

Il launcher prova a leggere il modello attualmente caricato in LM Studio da `/api/v1/models` e poi `/v1/models`.
Se lo trova, aggiorna `~/.hermes/config.yaml` e imposta `HERMES_INFERENCE_MODEL`.

Installazione come servizio user systemd:

```bash
mkdir -p ~/.config/systemd/user
cp scripts/hermes-hub-linux.sh ~/hermes-hub-linux.sh
chmod +x ~/hermes-hub-linux.sh
cp scripts/hermes-hub-linux.service ~/.config/systemd/user/hermes-hub.service
systemctl --user daemon-reload
systemctl --user enable --now hermes-hub.service
```

Se LM Studio gira su un altro host:

```bash
LM_STUDIO_BASE_URL=http://IP_DEL_SERVER:1234 ./scripts/hermes-hub-linux.sh
```

Profilo finale Ubuntu headless con vLLM:

```bash
HERMES_INFERENCE_PROVIDER=vllm \
VLLM_BASE_URL=http://127.0.0.1:8000 \
HERMES_INFERENCE_MODEL=nome-modello-vllm \
./scripts/hermes-hub-linux.sh
```

Oppure nel servizio systemd:

```ini
Environment=HERMES_INFERENCE_PROVIDER=vllm
Environment=VLLM_BASE_URL=http://127.0.0.1:8000
Environment=HERMES_INFERENCE_MODEL=nome-modello-vllm
```

Il servizio deve esporre sempre `http://SERVER:8642/v1` verso Hermes Hub e usare `API_SERVER_KEY=hermes-hub`, cosi' Android/Windows non cambiano configurazione quando si passa da Windows test a Ubuntu/vLLM.

Verifica media proxy:

```bash
curl -H "Authorization: Bearer hermes-hub" http://SERVER:8642/v1/capabilities
```

Per chat con file multimediali, `features` deve includere `media_proxy`, `media_register` e `visual_blocks_media_file`. Se una build Hermes non li espone ancora, portare la patch gateway usata nei test Windows prima di usare Ubuntu/vLLM in produzione.
