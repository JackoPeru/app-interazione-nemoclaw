# Visual Blocks golden screenshots

Questa directory contiene baseline unica per i renderer Visual Blocks.

Regole:

- Windows CI genera screenshot canonici.
- Android confronta gli stessi file con diff tolerance.
- Android non crea baseline autonoma.
- Fixture sorgente: `visual-blocks-fixture.json`.
- Aggiornare baseline solo quando cambia intenzionalmente il rendering.

File attesi futuri:

- `windows-visual-blocks-dark.png`
- `android-visual-blocks-dark-diff.json`

