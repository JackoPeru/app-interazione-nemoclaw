# Audit round 6 — plan 0.6.25

Stato: tracciamento fix audit post-0.6.24.

## Critici

- [x] **C1** Android `postJson` DELETE invia body — `MainActivity.kt:3624`. Skip body per DELETE.
- [x] **C2** Android `postJson` crea `OkHttpClient` per call — riuso `streamHttpClient` singleton.
- [x] **C3** AdminBridge `/v1/files/write` no concurrent lock — `SemaphoreSlim` per path.
- [x] **C4** Windows HomePage Send rapid click race — set `IsEnabled=false` + flag immediato.

## High

- [x] **H1** Android `toolCalls.forEach` Compose key=id — ChatStreamUi.kt:184.
- [x] **H2** Android `validBlocks.forEach` Compose key=id — ChatStreamUi.kt:79.
- [x] **H3** Android format locale US — `String.format(Locale.US, "%.1f", ...)`.
- [x] **H4** Android `Tab.entries.filterNot` cached con `remember`.
- [x] **H5** Windows WorkspaceRequestStore + AgentTaskStore cache parity con ChatArchiveStore.

## Med

- [x] **M1** Android `detectWorkspaceIntent` + `makeTitle` regex top-level cached.
- [x] **M2** Android `makeTitle` fallback "Nuova richiesta" se empty.
- [x] **M3** Android AlertDialog title `.take(60)`.
- [x] **M4** Android VisualBlocks JSON early reject id blank.
- [x] **M5** Windows App.UnhandledException log via Debug ma marker chiaro per future telemetry.

## Low

- [x] **L1** Android `widthIn(min=3.dp, max=3.dp)` → `width(3.dp)`.
- [x] **L2** Android `allowBackup="false"` manifest.
- [x] **L3** Android `ShimmerText`/expander infinite transition: deferred — refactor lifecycle non in scope, impact basso.

## Esclusi

- AppUpdateService resume download: richiede Range/append protocol.
- HomePage/MainActivity monolith refactor: scope grande.
- Light theme Android, i18n strings.xml.
- Windows AppSettingsStore migration TOCTOU: low impact single-user.

## Release

- [ ] **R1** Bump 0.6.24 → 0.6.25.
- [ ] **R2** Build assets.
- [ ] **R3** Commit, tag, push, gh release.
