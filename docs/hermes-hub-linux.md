# Hermes Hub su Ubuntu/Linux

Avvio manuale:

```bash
chmod +x scripts/hermes-hub-linux.sh
./scripts/hermes-hub-linux.sh
```

Default:

```text
Hermes API: http://0.0.0.0:8642/v1
LM Studio: http://127.0.0.1:1234/v1
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
