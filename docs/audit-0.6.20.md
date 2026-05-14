# Audit + plan Hermes Hub 0.6.20

Stato: tracciamento fix audit post-0.6.19.

## Critici

- [x] **A1** Android `OkHttpClient` singleton condiviso (ora new per request) — `ChatStream.kt:296`
- [x] **A2** `activeStreamJob` reset a null a fine stream — `ChatState.kt:18` + `MainActivity.kt` collect
- [x] **A3** `items(state.messages, key=)` per LazyColumn chat — `MainActivity.kt:740`
- [x] **A4** `rememberSaveable` su `selectedTab`, `pendingPrompt`, `pendingConversationId`, `sidebarOpen` — `MainActivity.kt:391-396`
- [x] **A5** AdminBridge body size limit (Kestrel `MaxRequestBodySize`) + check `request.Text.Length`
- [x] **A6** AdminBridge token fail-fast se env var vuoto

## Importanti

- [x] **B1** Cleanup warning compilatore `!!`/`?.` ridondanti — `ChatStream.kt:251,286,288,293,295,299` + `ChatStreamUi.kt:326,401`
- [x] **B2** `network_security_config.xml`: cleartext ristretto solo a hermes.local invece di base globale
- [x] **B3** Windows `MainWindow` unsubscribe `ChatArchiveStore.Changed` su `Closed`
- [x] **B4** Windows `HomePage` reset `_isSending` su `OperationCanceledException`
- [x] **B5** Windows `AppSettings.DemoMode` default `false`
- [x] **B6** Android `Brush.verticalGradient` memoizzato — `MainActivity.kt:705-717`
- [x] **B7** Android `state.visualBlocks.filter{}.forEach` memoizzato — `ChatStreamUi.kt`

## UX / qualità

- [x] **C1** Composer Android: autocorrect on via `KeyboardOptions`
- [x] **C2** Settings Android: toggle visibility API key field
- [x] **C3** Haptic feedback Android a fine generazione
- [x] **C4** Slash command list scroll se >max

## Release

- [ ] **R1** Bump 0.6.19 → 0.6.20 su tutti version field + AGENTS.md
- [ ] **R2** Build APK + Windows zip + AdminBridge zip + linux-helper zip
- [ ] **R3** Commit, tag, push, gh release create

## Esclusi

- Signing keystore release dedicato (richiede input utente, no keystore in repo)
- ProGuard/R8 minify (rischio rottura senza test estesi su device)
- Refactor MainActivity 5094 righe → moduli (scope troppo grande per singola release)
- AppColors theme migration (refactor pervasivo)
- DPAPI encrypt settings.json Windows (refactor non critico, credenziali già in Credential Locker)
