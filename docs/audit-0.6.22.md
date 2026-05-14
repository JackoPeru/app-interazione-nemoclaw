# Audit round 3 — plan 0.6.22

Stato: tracciamento fix audit post-0.6.21.

## Critici

- [x] **C1** Android `extractTextFromAnyJson` depth limit — `MainActivity.kt:3735+`. JSON nidificato malevolo → stack overflow. Cap depth a 10.
- [x] **C2** Android `state.messages.toList()` history cap — `MainActivity.kt:815`. Convo lunghissima = prompt esploso. `takeLast(30)`.
- [x] **C3** AdminBridge `File.Copy(path, backup)` exception handling — Program.cs `/v1/files/write`. Wrap try-catch.
- [x] **C4** Windows BitmapImage no `DecodePixelWidth` — HomePage.xaml.cs. Memory bomb su immagini giganti. Set `DecodePixelWidth = 720`.

## High

- [x] **H1** AdminBridge audit log lock — `File.AppendAllText` concorrente. Aggiungi `lock`.
- [x] **H2** AdminBridge `ProcessStartInfo` ArgumentList — quoting safety per fileName con spazi.
- [x] **H3** Android `renderInlineMarkdown` memoize — wrap `remember(text)` in `MarkdownText.Paragraph/Header/Bullet`.
- [x] **H4** Android gallery `loadRemoteBitmap` size cap + timeout — `BitmapFactory.Options.inSampleSize` + URLConnection timeout.
- [x] **H5** Android SettingsField/SettingsPasswordField maxLength — filtro `onValueChange` 2048 char.
- [x] **H6** Android `getSharedPreferences` cache — singleton accessor per CURRENT_SETTINGS_PREFS / CURRENT_ARCHIVE_PREFS.

## Med

- [x] **M1** Android `mergeTextDelta`: review finale — logica esistente (`startsWith`/`endsWith`/equality) gia' copre i casi SSE replay realistici. False alarm audit, no code change.
- [x] **M2** Android Composer `heightIn` scale by `fontScale` — bounds dinamici per font scale > 100%.
- [x] **M3** Windows GatewayCredentialStore distinguish exceptions — log `CryptographicException` vs not-found.
- [x] **M4** Windows BitmapImage `MaxWidth` — pair con `MaxHeight`, evita tall narrow OOM.
- [x] **M5** Windows HttpClient redirect explicit — `HttpClientHandler.AllowAutoRedirect = false` su gateway client; o `MaxAutomaticRedirections = 0`.

## Low

- [x] **L1** Windows `_isSending` `volatile` — fragile pattern even on UI thread.

## Esclusi (gia' gestiti o non scope)

- OkHttp `readUtf8Line` per-read timeout: callTimeout 60min copre. Server malevolo LAN improbabile.
- Windows Done event ordering: edge case server, no fix.
- Tab.Chat enum reorder: gia' runCatching.

## Release

- [ ] **R1** Bump 0.6.21 → 0.6.22.
- [ ] **R2** Build APK + Windows + AdminBridge + linux zip.
- [ ] **R3** Commit, tag, push, gh release.
