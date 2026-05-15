# Audit round 5 — plan 0.6.24

Stato: tracciamento fix audit post-0.6.23. UI + code.

## Critici

- [x] **C1** Android `AppColors.Muted #A2ADBF` contrast 3.8:1 < WCAG AA. Bump piu' chiaro (`#C5D1DF` o simile, ratio >= 4.5:1).
- [x] **C2** Android data classes `@Immutable` — `ChatMessage`, `VisualBlock`, `StreamingState`, `ChatStreamStats`, `ToolCallState`, `GalleryImage`, `TableColumn` ecc.
- [x] **C3** Windows `GatewayService.ReadAsStringAsync` no body cap. `HttpClient.MaxResponseContentBufferSize = 10MB` o pre-check Content-Length.

## High

- [x] **H1** Android touch target < 48dp su expand icons — wrap clickable Box 48dp.
- [x] **H2** Android Streaming `animateContentSize()` per smooth recompose.
- [x] **H3** Android markdown code block `horizontalScroll`.
- [x] **H4** Windows `GatewayCredentialStore.SaveSecret`/`DeleteSecret` usa `SharedVault.Value`.
- [x] **H5** Windows HomePage event handler leak `OnNavigatedFrom` cleanup.
- [x] **H6** Windows `async void` button handler → try-catch wrap.

## Med

- [x] **M1** Android empty states CTA (Archive, Jobs, Recent).
- [x] **M2** Android error `Text` `liveRegion = Assertive`.
- [x] **M3** Windows `MutedTextBrush #FFA2ADBF` contrast — bump.
- [x] **M4** Windows sidebar collapse animation: N/A — GridLength richiede Composition API; deferred.
- [x] **M5** Windows `ChatArchiveStore` cache + invalidate on `Changed`.
- [x] **M6** Windows StreamingBubble `OnNavigatedFrom` stop shimmer timer.

## Low

- [x] **L1** Android light theme: N/A — refactor `ChatClawTheme` con dynamic schema fuori scope.
- [x] **L2** Windows sidebar button `ToolTipService.ToolTip` per chat recenti.
- [x] **L3** Windows AppUpdateService timeout 5min → 30min (slow conn large asset).

## Esclusi

- Android `ChatStateHolder` Parcelable: refactor ViewModel grande.
- Typography MaterialTheme scale: cosmetic, scope grande.
- Spacing constants: cosmetic.
- i18n strings.xml: refactor enorme.
- MSIX runFullTrust: necessario.

## Release

- [ ] **R1** Bump 0.6.23 → 0.6.24.
- [ ] **R2** Build assets.
- [ ] **R3** Commit, tag, push, gh release.
