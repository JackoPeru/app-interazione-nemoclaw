# agent.md

## Scopo progetto

Hermes Hub e' client Windows + Android per comunicare con Hermes Agent API Server su home-server. Deve offrire chat stile ChatGPT, modalita agente, archivio locale, Jobs/Runs, dashboard server e update in-app.

## Decisione corrente

Hermes Visual Blocks v1 implementato lato client: spiegazioni visuali statiche e sicure nella chat testuale.

- Contratto: `docs/visual-blocks-schema.md` e `config/visual-blocks.schema.json`.
- Output Hermes: `output_text` autosufficiente + `visual_blocks_version: 1` + `visual_blocks`.
- Tipi: `markdown`, `code`, `table`, `chart`, `diagram`, `image_gallery`, `callout`.
- Sicurezza: niente HTML, niente JS, niente SVG client-side; media solo tramite Hermes proxy.
- Versioning: client dichiara min/max supportati e mode `auto|always|never`.
- V1 no streaming visuale: client mostra stato attesa e renderizza a response completa.
- Sezioni Video e News avviate: aree separate dalla chat per futuri tool Hermes di generazione video e ricerche/briefing news. Backlog in `prossime implementazioni.md`.

## Limiti Visual Blocks v1

- Max 20 blocchi per messaggio.
- Max 500 KB payload blocchi serializzato.
- Tabelle: 12 colonne, 100 righe.
- Chart: `bar|line`, 8 serie, 200 punti per serie, `summary` obbligatorio.
- Diagrammi: Mermaid source + media proxy opzionale, PNG diagram max 500 KB.
- Gallery: 12 immagini, max 2 MB per immagine lato proxy.

## Regole mantenimento

- Aggiornare questo file quando cambiano scopo, contratti API, storage, sicurezza, renderer o limiti.
- History verso Hermes resta solo testo umano: mai rimandare JSON Visual Blocks.
- Archivio deve restare compatibile con messaggi vecchi solo testo.
- Tipi Visual Blocks generati da quicktype: controllare sempre discriminator `type` ed enum wire-value tra C# e Kotlin con `scripts/verify-visual-blocks-contract.ps1`.
- Golden screenshot: una sola baseline versionata in `tests/golden/`, generata da Windows CI; Android confronta con tolleranza e non crea baseline autonoma.
