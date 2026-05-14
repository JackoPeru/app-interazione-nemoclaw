# Audit round 2 — plan 0.6.21

Stato: tracciamento fix audit post-0.6.20.

## Critici

- [x] **C1** Windows `AppUpdateService`: cap su `Content-Length` (rifiuta > 500MB), sanitize `assetName` (rifiuta path separators), evita follow redirect verso domini esterni a GitHub. Path traversal via name + disk exhaustion DoS via Content-Length manipolato.
- [x] **C2** Windows JSON store: scrittura atomica via temp+rename (`File.Replace`) in `AppSettingsStore`/`ChatArchiveStore`/`AgentTaskStore`. Crash mid-write = file corrotto = chat/settings persi. Backup `.bak` automatico.

## High

- [x] **H1** Android I/O off main thread: `loadSettings`/`loadConversations`/`loadConversation` chiamati dentro `remember{}` o `LaunchedEffect` senza `withContext(Dispatchers.IO)`. Refactor: `produceState` + `withContext(IO)` per load asincroni. SaveSettings sempre off main.
- [x] **H2** Android AndroidKeyStore race: `getOrCreateGatewaySecretKey` non sincronizzato. Avvolgi in `synchronized` su lock object dedicato.
- [x] **H3** AdminBridge symlink/junction escape: `ResolveSafePath` usa `Path.GetFullPath` + `StartsWith` string. Symlink dentro root → escape. Fix: canonicalizza con `Directory.ResolveLinkTarget`/`FileInfo.LinkTarget`, rifiuta `FileAttributes.ReparsePoint`, verifica prefix su entrambi i FullName.
- [x] **H4** AdminBridge token length guard: `token["Bearer ".Length..]` può throw se token < 7 char o non Bearer. Aggiungi length check prima di substring.
- [x] **H5** Windows UI thread I/O: `AppSettingsStore.Load` / `ChatArchiveStore.Load` / `AgentTaskStore.Load` da MainWindow constructor + handler. Async load già via `await` dove possibile; per startup costretto sync ma OK piccoli file. Documenta scope.

## Med

- [x] **M1** Android stream `accumText`/`accumThink` cap: hard limit ~2MB con truncate + warning. Evita OOM su risposta gigante.
- [x] **M2** Send button double-press: guard atomic in `onSend` callback. Check `state.sending` due volte (entry + dentro synchronized).
- [x] **M3** Markdown inline parser O(n²): aggiungi iteration limit (max 100 stili inline per blocco) + truncate input testo > 200KB.
- [x] **M4** OkHttp HttpLoggingInterceptor in build debug: aggiungi `BuildConfig.DEBUG ? loggingInterceptor : null`.
- [x] **M5** Settings save: normalizza gateway URL su save (trim + remove trailing slash) lato Android.
- [x] **M6** Windows ChatStream `catch { document = null }`: log debug invece di swallow silenzioso.
- [x] **M7** AdminBridge `ReadAllLinesAsync` con `Encoding.UTF8` esplicito.

## Esclusi

- Retry/backoff SSE 503/429: LAN, non utile.
- MSIX runFullTrust: necessario per WindowsAppSDK unpackaged.
- MSIX cert reale: richiede certificate user-side.
- Signature verify update binari: GitHub HTTPS + tag verify ok per scope LAN.

## Release

- [ ] **R1** Bump 0.6.20 → 0.6.21 su tutti version field + AGENTS.md
- [ ] **R2** Build APK + Windows zip + AdminBridge zip + linux-helper zip
- [ ] **R3** Commit, tag, push, gh release create
