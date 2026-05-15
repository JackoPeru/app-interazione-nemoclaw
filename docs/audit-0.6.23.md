# Audit round 4 — plan 0.6.23

Stato: tracciamento fix audit post-0.6.22.

## Critici

- [x] **C1** Android `extractAssistantText` ricorsione SSE no depth cap — `MainActivity.kt:3827`. Add depth counter, cap 10.
- [x] **C2** AdminBridge `CHATCLAW_ADMIN_TIMEOUT` negativo crash — Program.cs:277. `Math.Max(1, timeout)`.
- [x] **C3** AdminBridge `Process.Kill(true)` no try-catch — Program.cs:418. Wrap try-catch.
- [x] **C4** Windows HomePage `bubble.AppendText` da thread non-UI — HomePage.xaml.cs:300. `DispatcherQueue.TryEnqueue`.
- [x] **C5** Windows `GatewayService.HttpClient.Timeout=20s` aborta stream lunghi — fix per-request CTS.
- [x] **C6** Windows ChatStream SSE `dataBuilder` no size cap — cap 10MB.

## High

- [x] **H1** Android `chatState` non rememberSaveable — process death = draft+messages persi. Flag draft saveable, ChatStateHolder defer (Job non parcelable).
- [x] **H2** Android LazyColumn hashCode collision — `ChatMessage` aggiungi `id` field, key=id.
- [x] **H3** Android `TasksScreen` `loadTasks` sync su `remember{}` — LaunchedEffect + IO.
- [x] **H4** Android NewsScreen/VideoScreen dead code post-return — cleanup.
- [x] **H5** Windows `GatewayCredentialStore` `new PasswordVault()` per call — cache static.
- [x] **H6** Windows MainWindow `Closed` re-entrancy — flag check.

## Med

- [x] **M1** Android AlertDialog delete dismiss on outside tap — `DialogProperties(dismissOnBackPress=false, dismissOnClickOutside=false)`.
- [x] **M2** Android `fontScale` NaN coerceIn — `if (!isFinite)` fallback.
- [x] **M3** Android `saveConversations` `.apply()` → `.commit()` per save critici. Valuta.
- [x] **M4** Android StreamingBubble `liveRegion = Polite` per TalkBack.
- [x] **M5** Windows `/v1/logs/tail` reverse-read invece di full + TakeLast.

## Low

- [x] **L1** Android `makeTitle` long prompt sanitize newline + truncate.
- [x] **L2** Windows StreamingBubble idempotent stop shimmer.

## Release

- [ ] **R1** Bump 0.6.22 → 0.6.23.
- [ ] **R2** Build APK + Windows + AdminBridge + linux zip.
- [ ] **R3** Commit, tag, push, gh release.
