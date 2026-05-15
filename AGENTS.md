# AGENTS.md

## Obiettivo

Creare app chat stile ChatGPT per comunicare con home-server che ospita Hermes Agent.

Target:

- App desktop Windows.
- App Android.
- UI moderna chatbot: dark premium operator console, composer largo, menu `+`, modalita `Chat`/`Agente`.
- Nome visibile app: `Hermes Hub`.
- Compatibilita Android: `applicationId` resta `com.nemoclaw.chat`.
- Backend primario: Hermes Agent API Server su `http://hermes.local:8642/v1`.
- Direzione nuova: Hermes Hub e' client operativo per Hermes Agent, non console del vecchio backend.

## Regola Git

Non fare commit o push su GitHub a meno che Matteo lo chieda esplicitamente.

Repo remoto:

```text
https://github.com/JackoPeru/app-interazione-nemoclaw.git
```

Branch usato:

```text
main
```

Ultimo push release fatto su richiesta utente:

```text
v0.6.28 Release Hermes Hub 0.6.28
```

## Regola Memoria

Aggiornare questo file ogni volta che cambia qualcosa di importante nel progetto:

- obiettivo o scope;
- stato Windows/Android;
- decisioni tecniche;
- impostazioni default;
- comandi build/test;
- regole Git;
- bug noti;
- TODO/prossimi passi;
- preferenze utente.

Non lasciare `AGENTS.md` obsoleto dopo modifiche rilevanti.

## Release Corrente

Hermes Hub 0.6.28 (Windows + Android):

Fix auth Android:
- Android chat streaming ora se riceve `401 invalid_api_key` con una API key salvata ritenta automaticamente senza header `Authorization`. Serve quando Hermes/LM Studio gira senza key ma l'app conserva una key vecchia nel Keystore.
- Settings Android aggiunge bottone `Cancella API key`, per rimuovere solo il segreto Hermes senza fare reset totale delle impostazioni.

## Release 0.6.27

Hermes Hub 0.6.27 (Windows + Android):

Run post-audit:
- Android `demoMode` default/reset passato a `false`. Se Hermes non risponde, l'app mostra errore reale invece di mascherarlo con fallback locale.
- Verifiche passate: Android `compileDebugKotlin`, Android `lintDebug`, Windows `dotnet build`, AdminBridge `dotnet build`.

## Release 0.6.26

Hermes Hub 0.6.26 (Windows + Android):

Audit round 7. Tutti i fix in `docs/audit-0.6.26.md`.

Android critici/importanti:
- AES-GCM secret Hermes con AAD stabile; debug HTTP logging redige `Authorization`/`Cookie`.
- FileProvider limitato a `exports/`; update APK salvati in `externalFilesDir/exports`.
- Titoli conversazione filtrano bidi control chars; back di sistema torna tra tab o chiude sidebar invece di uscire.
- Offline state via `ConnectivityManager`; banner immediato e send bloccato se rete assente.
- Responses API SSE con retry/backoff prima del fallback Chat Completions; fallback loggato e mostrato come status.
- Manifest con `ACCESS_NETWORK_STATE` + `<queries>` per picker/camera/browser.
- Tool result gia' monospace (N/A markdown injection); `prettifyJson` troncato a 20k char.
- Messaggi selezionabili/copiabili con `SelectionContainer`; Settings/Profile con `imePadding`.
- Adaptive icon Android 13 con monochrome layer; StrictMode debug; lint block configurato.
- Defer dichiarati: tablet WindowSizeClass e scrollbar chat Compose (scope basso/grosso come audit).

Windows/AdminBridge critici/importanti:
- HomePage messaggi spostati da `StackPanel` a `ListView` virtualizzata con collection `MessageViewModel`.
- PromptBox: `MaxLength=50000`, spellcheck, automation name, drag/drop file, paste immagine intercettato.
- Errori stream mostrati in `InfoBar`; messaggi con context menu `Copia`; shortcut `Ctrl+N` nuova chat e `Ctrl+L` pulisci chat.
- ChatStream SSE hard cap 50MB totale; MarkdownRenderer cap 200k char/500 blocchi.
- VisualBlocks parse/deserialization con `MaxDepth=16`.
- AdminBridge: audit log rotation 10MBx5, rate limiting 60 req/min/IP, CORS whitelist Hermes, `/v1/reload`, shutdown log.
- Windows: posizione/dimensione finestra persistenti, `RequestedTheme=Dark`, file logger release `%LOCALAPPDATA%\ChatClaw\logs\app.log`, high contrast brush override, sidebar tooltips/automation.
- AppSettingsStore migration tollerante a race I/O.
- Defer dichiarati: WindowsAppSDK version bump e touch/pen custom handlers.

## Release 0.6.25

Hermes Hub 0.6.25 (Windows + Android):

Audit round 6. Tutti i fix in `docs/audit-0.6.25.md`.

Critici:
- Android `postJson` ora riusa `apiHttpClient` singleton lazy (era `OkHttpClient.Builder().build()` per call). Connection pool condiviso, fd risparmiati.
- Android `postJson` DELETE: verificato no body (gia' corretto via `.delete()`).
- AdminBridge `/v1/files/write`: `PathWriteLocks.Get(path)` (`SemaphoreSlim` per path con `ConcurrentDictionary`). Write paralleli stesso path serializzati.
- Windows HomePage `SendCurrentPromptAsync` set `_isSending=true` + `IsEnabled=false` PRIMA di prompt validation. No race su rapid Send click multipli.

High:
- Android ChatStreamUi `validBlocks.forEach` e `toolCalls.forEach` ora avvolti in `androidx.compose.runtime.key(id)`. Compose recomposition stabile.
- Android `"%.1f".format(...)` → `String.format(java.util.Locale.US, "%.1f", ...)` su ChatStreamUi. Locale IT non corrompe piu' decimali stats/timings (era asimmetrico con MainActivity gia' corretto).
- Android `Tab.entries.filterNot{...}` ora `remember`-ato nella NavigationBar. No alloc per recompose.
- Windows `WorkspaceRequestStore` e `AgentTaskStore` ora con cache statico `List<...>?` + `lock` + invalidate su SaveAll. Parita' con `ChatArchiveStore`.

Med:
- Android `Regex("\\s+")` ora `MULTI_WHITESPACE_REGEX` top-level. Non compila piu' per call.
- Android `makeTitle` fallback `"Nuova richiesta"` se input solo whitespace/punctuation.
- Android AlertDialog delete: title runtime `.replace('\n', ' ')` + truncate 60 char. Lunghi titoli no overflow.
- Android VisualBlock: verificato `isValidVisualBlock` gia' rifiuta `id.isBlank()`.
- Windows `App.UnhandledException`/domain/task ora con tag marker `telemetry/...` + stack trace + IsTerminating flag. Pronto per hookup futuro telemetry.

Low:
- Android `Modifier.widthIn(min=3.dp, max=3.dp)` → `Modifier.width(3.dp)` (CalloutBlock).
- Android `allowBackup` verificato gia' `false` in AndroidManifest.
- Android ShimmerText/InfiniteTransition conditional: deferred (refactor lifecycle).

## Release 0.6.24

Audit round 5 — focus UI + code. Tutti i fix in `docs/audit-0.6.24.md`.

Critici:
- Android `AppColors.Muted` da `#A2ADBF` (3.8:1) a `#C8D2E0` (~6.5:1 su Background). Conformita' WCAG AA per testo secondario. Faint da `#6B7585` a `#8892A2`.
- Android data class `@Immutable` su `ChatMessage`, `VisualBlock`, `VisualTableColumn`, `VisualChartSeries`, `VisualChartPoint`, `VisualGalleryImage`, `ChatStreamStats`, `ToolCallState`, `StreamingState`. Compose puo' skip recomposition stabile.
- Windows `GatewayService.HttpClient` con `MaxResponseContentBufferSize = 10MB`. Server malevolo o response gigante non OOM-a app.

High:
- Android touch target su expander rows: `heightIn(min = 48.dp)` + icone bumped a 18-20dp. Conformita' Material 48dp.
- Android `StreamingBubbleView` con `animateContentSize()`. Recompose token-per-token smooth, no flicker.
- Android markdown code block: `softWrap = false` + `horizontalScroll(rememberScrollState())`. Codice lungo non clippa silenzioso.
- Windows `GatewayCredentialStore.SaveSecret`/`DeleteSecret` ora usa `SharedVault.Value` cached + try-catch + log.
- Windows `HomePage.Unloaded` handler: unsubscribe `PromptBox.TextChanged` + chiude `_slashPopup.IsOpen = false` + stop bubble shimmer.
- Windows `App.xaml.cs` global handler: `UnhandledException` (UI) + `AppDomain.CurrentDomain.UnhandledException` + `TaskScheduler.UnobservedTaskException`. async void handler che crash non spengono app.

Med:
- Android empty states Archive/Jobs/Recent ora con titolo bold + CTA descrittiva ("Tocca + per iniziarne una.").
- Android error `Text` con `liveRegion = Assertive`. TalkBack annuncia errori stream subito.
- Windows `MutedTextBrush` da `#FFA2ADBF` a `#FFC8D2E0`. WCAG AA su dark bg.
- Windows `ChatArchiveStore` cache statico `List<ConversationRecord>?` con `lock`, invalidato su SaveAll. No piu' reparse JSON ad ogni `Load()`/`Find()`/`Recent()`.
- Windows HomePage track `_currentStreamingBubble`, `Unloaded` stop shimmer su navigation away.

Low:
- Windows `AppUpdateService.HttpClient.Timeout` 5min → 30min (asset > 100MB su conn lenta).
- Windows sidebar dynamic buttons: `ToolTipService.SetToolTip` + `AutomationProperties.SetName` per accessibility.

Esclusi (scope troppo grande):
- Windows sidebar collapse animation (WinUI 3 GridLength richiede Composition API).
- Android light theme (refactor `ChatClawTheme` con dynamic schema).
- `ChatStateHolder` Parcelable (refactor ViewModel + SavedStateHandle).
- i18n strings.xml extraction, Material typography scale, spacing constants.

## Release 0.6.23

Audit round 4. Tutti i fix in `docs/audit-0.6.23.md`.

Critici:
- Android `extractAssistantText` con depth cap 10. Stop overflow su SSE nidificato malevolo.
- AdminBridge `CHATCLAW_ADMIN_TIMEOUT`/`MAX_READ_BYTES`/`MAX_REQUEST_BYTES`/`MAX_WRITE_CHARS` clampati con `Math.Max`. Niente piu' `ArgumentOutOfRangeException` su env var negativo.
- AdminBridge `Process.Kill(true)` in try-catch (`InvalidOperationException`, `Win32Exception`). stdin/stdout read fault-tolerant via try-catch su `await stdoutTask`/`await stderrTask`.
- Windows ChatStream SSE `dataBuilder` cap 10MB per evento. Server malevolo non puo' piu' OOM stream loop.
- Windows `GatewayService.HttpClient.Timeout` 20s → 5min. Chat non-streaming su modelli grandi non si abortisce piu' a meta'.

High:
- Android `chatState.draft` persistito via `rememberSaveable savedDraft` + `LaunchedEffect` sync. Process death non perde piu' draft.
- Android `ChatMessage` ora ha `id: String` UUID stabile + LazyColumn `items(messages, key=id)`. Niente piu' collision hashCode su messaggi identici. JSON `readMessages`/`writeMessages` round-trip id.
- Android `TasksScreen.loadTasks` ora dentro `LaunchedEffect` + `withContext(Dispatchers.IO)`. Niente piu' I/O sync su Composition thread.
- Android: rimosso dead code 90+90 righe in `VideoScreen`/`NewsScreen` (codice post-return mai eseguito).
- Windows `GatewayCredentialStore.PasswordVault` cached come `Lazy` singleton invece di `new` per call.
- Windows `MainWindow_Closed` guarda con flag `_closing` + try-catch per re-entrancy safety.

Med:
- Android AlertDialog delete `DialogProperties(dismissOnClickOutside=false)`. Tap fuori non chiude piu' modale di conferma.
- Android `fontScale` `isFinite()` check con fallback 1f. NaN/Infinity da settings corrotti non rompe `coerceIn`.
- Android `StreamingBubbleView` `semantics { liveRegion = Polite }` per TalkBack live updates.
- AdminBridge `/v1/logs/tail` ora usa `ReadLastLinesAsync` che fa seek-from-end + read backward in chunk 8KB. Niente piu' allocazione di tutto il file.

Low:
- Android `makeTitle` sanitize char non-stampabili + collapse whitespace + filter caratteri pericolosi.

Note: C4 (DispatcherQueue Windows) verificato come falso positivo. `await foreach` preserva sync context UI in WinUI 3.

## Release 0.6.22

Audit round 3 + hardening. Tutti i fix elencati in `docs/audit-0.6.22.md`.

Critici:
- Android `extractTextFromAnyJson` (ChatStream.kt) e `extractJsonText` (MainActivity.kt) ora hanno depth cap a 10. JSON profondo malevolo non causa piu' stack overflow.
- Android `streamChatRequest` ora invia `messages.takeLast(CHAT_HISTORY_MAX_MESSAGES=30)` invece di full history. Convo lunghe non esplodono il prompt.
- AdminBridge `/v1/files/write`: `File.Copy` backup in try-catch (IOException/UnauthorizedAccessException). 503 se backup fallisce, 500 su write fail.
- Windows BitmapImage `RenderDiagram`/`RenderGallery` con `DecodePixelWidth = 720` + `MaxWidth = 720`. Immagini 10000x10000 non possono piu' OOM-are l'app.

High:
- AdminBridge `AuditLog.Write` ora con `lock` + try-catch su `File.AppendAllText`. Audit log non corrompe piu' su richieste concorrenti.
- AdminBridge `CommandRunner.RunAsync` usa `ProcessStartInfo.ArgumentList` invece di string. `fileName` con spazi sicuro, no shell injection.
- Android `MarkdownText`: `renderInlineMarkdown` ora memoizzato per `Paragraph`/`Header`/`Bullet` via `remember(block.text, color)`. Streaming bubble non ricostruisce AnnotatedString ad ogni frame.
- Android `loadRemoteBitmap`: cap 10MB advertised + 10MB letti + scaling via `inSampleSize` per max 2048px lato + `finally { disconnect() }`. Immagini grandi non OOM-ano, no socket leak.
- Android `SettingsField`/`SettingsPasswordField`: `take(SETTINGS_FIELD_MAX_LENGTH=2048)` su onValueChange. Paste 1MB non grippa UI.
- Android prefs cache: `ConcurrentHashMap` su `getSharedPreferences` per nome + flag `migratedPrefs` per evitare migrate retry. Riduce I/O ripetuto.

Med:
- Android Composer `heightIn` ora scala con `LocalDensity.current.fontScale` (composer cresce con font scale 150%).
- Windows `GatewayCredentialStore.LoadSecret` distingue `COMException` / `CryptographicException` / generic e logga via `Debug.WriteLine`.
- Windows `GatewayService.HttpClient` ora con `HttpClientHandler.AllowAutoRedirect = false` esplicito. Niente piu' redirect verso host non validati.
- Windows BitmapImage `MaxWidth = 720` accoppiato a `MaxHeight` (no tall narrow image OOM).

Low:
- Windows `_isSending` ora `volatile` (pattern memoria meno fragile).

Note: `mergeTextDelta` audit segnalato come edge-case, ma la logica esistente gia' copre i casi standard SSE replay (`startsWith`, `endsWith`). Skip.

## Release 0.6.21

Audit round 2 + hardening. Tutti i fix elencati in `docs/audit-0.6.21.md`.

Critici:
- Windows `AppUpdateService`: hardening download. Whitelist host (`github.com`/`githubusercontent.com`), cap dimensione asset 500MB (sia da `Content-Length` sia da bytes letti), sanitize asset name (rifiuta path separators / `..`), max redirect 5, timeout 5 min, check destination dentro updates directory.
- Windows JSON store: scrittura atomica via `AtomicJsonFile` (temp + `File.Replace` con backup `.bak`). Crash mid-write non corrompe piu' `settings.json`/`conversations.json`/`tasks.json`/`workspace.json`.

High:
- Android: I/O off main thread su path caldo. `saveConversationExchange` + `saveWorkspaceRequest` + load conversazione in `LaunchedEffect` ora dentro `withContext(Dispatchers.IO)`. SaveSettings/Reset usa `chatScope.launch(Dispatchers.IO)`.
- Android: `getOrCreateGatewaySecretKey` ora `synchronized(gatewaySecretKeyLock)`, evita race che generava chiavi duplicate.
- AdminBridge: `ResolveSafePath` canonicalizza con `DirectoryInfo.ResolveLinkTarget`/`FileInfo.ResolveLinkTarget`, rifiuta `FileAttributes.ReparsePoint`. Symlink/junction non possono piu' uscire dalle root.
- AdminBridge: token Bearer ora con length guard prima di `[7..]`, evita IndexOutOfRange e timing leak su Authorization header malformato.

Med:
- Android stream: `accumText`/`accumThink` cap 2_000_000 char con truncate marker. Niente piu' OOM su risposta gigante.
- Android send button: pre-guard `state.activeStreamJob == null` + flag `sending` settato prima di append/state mutation, riduce finestra double-send.
- Android markdown inline parser: cap 200KB input + max 500 stili inline per blocco, blocca pattern adversarial O(n²).
- Android OkHttp: `HttpLoggingInterceptor` (HEADERS) attivo solo in build debug via reflection (no dep release).
- Android settings: gateway/admin/inference URL ora trimmati e senza trailing slash su save (`normalizeUrl`).
- Windows ChatStream: `catch (JsonException)` ora logga in `Debug.WriteLine` invece di swallow silenzioso.
- AdminBridge: `ReadAllLinesAsync` con `Encoding.UTF8` esplicito.

## Release 0.6.20

Audit + hardening post-0.6.19. Tutti i fix elencati in `docs/audit-0.6.20.md`.

Critici risolti:
- Android `OkHttpClient` ora singleton modulo (era new per request → fd waste + zero connection pool).
- Android `LazyColumn` messaggi con `key=` stabile via `itemsIndexed` (no recomposition mismatch su append).
- Android UI state rotation/process-death safe: `selectedTab`, `pendingPrompt`, `pendingConversationId`, `sidebarOpen` via `rememberSaveable`.
- AdminBridge: limite body request Kestrel (`CHATCLAW_ADMIN_MAX_REQUEST_BYTES`, default 4MB) + check `MaxWriteChars` su `/v1/files/write` + check size su `/v1/logs/tail` + fail-fast se `CHATCLAW_ADMIN_TOKEN` non set.

Importanti:
- Android: cleanup warning compilatore `!!`/`?.` ridondanti, `KeyboardOptions.autoCorrectEnabled`.
- Android: `Brush.verticalGradient` memoizzato in `ChatScreen`.
- Android: lista `validBlocks` memoizzata per recompose stream.
- Android: `network_security_config.xml` con `base-config` esplicito + commento doc strategia (cleartext LAN per design).
- Windows: `MainWindow` unsubscribe `ChatArchiveStore.Changed` su `Closed`.
- Windows: `DemoMode` default `false` (era `true`, mascherava errori reali).

UX:
- Android composer: autocorrect + capitalizzazione frasi attivi (`KeyboardCapitalization.Sentences`).
- Android settings: API key con toggle visibilita' (icona occhio).
- Android: haptic feedback `LongPress` a fine generazione stream.
- Android: `SlashCommandList` scrollabile con `heightIn(max=260.dp) + verticalScroll`.

Esclusi (richiedono input/refactor pervasivo):
- Signing keystore release dedicato (necessita keystore vero non in repo).
- ProGuard/R8 minify (rischio rottura senza test su device).
- Refactor MainActivity.kt 5000+ righe → moduli.
- Migrazione AppColors → `MaterialTheme.colorScheme`.

## Release 0.6.19

Hermes Hub 0.6.19 (Windows + Android):

- Android composer: rimosso gap residuo tra textbox e tastiera (padding bottom del Row composer passato da 6.dp a 0.dp), input flush sopra IME.
- Android streaming activity: rimossa percentuale fittizia animata (`activityProgressPercent`, basata solo su una fase 0→1 su 90s, mostrava 42% appena partita e non riflette il vero prompt processing di LM Studio). Sostituita con indicatore reale derivato dallo stato stream: `prompt…` shimmer in attesa del primo token, `reasoning N tok` durante reasoning, `N tok` durante generazione, `tool…` per tool pending, `Completato` a fine. Etichetta `Fase` non mostra piu' la percentuale.

## Release 0.6.18

Hermes Hub 0.6.18 (Windows + Android):

- Android sidebar: tap sul logo Hermes in alto a sinistra apre drawer stile ChatGPT con nuova chat, Archivio, Jobs e lista chat recenti apribili. Archivio e Jobs rimossi dalla bottom nav ma restano accessibili dal drawer.
- Android streaming parser: parsing Responses/Chat Completions piu' robusto per eventi finali annidati (`output_item.done`, `response.completed`, `content[]`, `output[]`) e deduplica del testo quando Hermes manda sia delta live sia risposta finale completa.

## Release 0.6.17

Hermes Hub 0.6.17 (Windows + Android):

- Android activity header: rimosso testo tecnico lungo (`Hermes Chat Completions connesso...`) dalla riga chiusa. Ora mostra solo stato breve animato + freccia + percentuale live; shimmer attraversa il testo da sinistra verso destra.

## Release 0.6.16

Hermes Hub 0.6.16 (Windows + Android):

- Memoria Hermes condivisa: app Android/Windows inviano contesto Hermes Hub e metadata `memory_policy.scope=shared-hermes-agent-memory`, `share_with_cli=true`. Chat Completions fallback include system message. Video/News/Jobs dichiarano `workspace` e memoria condivisa cosi Hermes puo' usare stessa memoria/profilo di CLI, app e jobs.
- Android streaming activity: pannello espandibile `Attivita Hermes` sempre durante generazione, anche dopo il primo token: stato connessione/processamento, reasoning live, generazione testo, tool call, argomenti e risultati tool.
- Streaming piu' rapido: Android e Windows non fanno piu' probe `/capabilities` prima di ogni messaggio; tentano direttamente `/v1/responses` e fanno fallback solo se serve. Android stop aggiorna subito UI con `Interruzione richiesta` mentre cancella lo stream OkHttp.

## Release 0.6.15

Hermes Hub 0.6.15 (Windows + Android):

- Immagini in chat: Android renderizza `visual_blocks` di tipo `image_gallery` caricando bitmap da media proxy Hermes `/v1/media/...`; Windows gia' renderizza image gallery e ora dichiara esplicitamente supporto immagini nelle istruzioni/metadata.
- Agent instructions: chat/agent chiedono a Hermes di inviare immagini solo come `image_gallery` con `media_url` proxy, `alt` e `caption`, rifiutando `file://`, `data:` e URL esterni diretti.
- Fallback visuale Android: i prompt con `immagine`, `image` o `foto` attivano i Visual Blocks anche in fallback locale.

## Release 0.6.14

Hermes Hub 0.6.14 (Windows + Android + Linux helper):

- Update UX: Android e Windows leggono il body della GitHub Release e mostrano una stringa `Novita'` nel riquadro Aggiornamenti, vicino a scarica/installa update.
- Windows chat: Enter invia sempre il messaggio; Shift+Enter inserisce newline manuale nel composer.
- Linux server helper: aggiunti `scripts/hermes-hub-linux.sh`, `scripts/hermes-hub-linux.service` e `docs/hermes-hub-linux.md` per esporre Hermes API su Ubuntu/Linux (`0.0.0.0:8642`), rilevare modello LM Studio caricato e avviare `hermes gateway run --replace`.
- Android Video/News: sezioni rese feed/aggregatori persistenti con Jobs Hermes, stream/download URL, feedback e riconoscimento automatico da chat primaria.
- Android chat empty state: gradiente amber piu' evidente e lungo circa il doppio.

## Release 0.6.13

Hermes Hub 0.6.13 (Windows + Android):

- Android chat empty state: il gradiente amber parte dal contenitore chat intero e si estende verso l'alto fuori dallo schermo; topbar logo/nome e azioni `Nuova`/`Chat` restano nella stessa posizione ma senza sfondo/pill.

## Release 0.6.12

Hermes Hub 0.6.12 (Windows + Android):

- Android keyboard: composer ancorato sopra la tastiera con `imePadding` solo sulla barra input, senza spostare tutta la chat e senza vuoto verticale eccessivo.
- Android settings: slider `Dimensione caratteri` continuo 85%-125%; percentuale cliccabile/editabile a mano e confermabile da tastiera.
- Android UI: vecchie card/vignette nelle sezioni convertite in righe/pannelli flat con separatori dritti, stile impostazioni Android; niente box arrotondati per i blocchi informativi principali.

## Release 0.6.11

Hermes Hub 0.6.11 (Windows + Android):

- Android settings: aggiunto slider `Dimensione caratteri` persistente (`fontScale` 85%-125%) con anteprima immediata; la scala viene applicata all'intera UI tramite `LocalDensity`.
- Android UI: sostituito il rendering delle vecchie card/vignette con pannelli premium piatti, bordo sottile, raggio basso e zero elevation; mantenuta bubble utente distinta ma meno giocattolosa.
- Android keyboard: composer resta aderente alla tastiera usando `adjustResize` senza doppio `imePadding`.

## Release 0.6.10

Hermes Hub 0.6.10 (Windows + Android):

- Android chat UI: composer compatto stile ChatGPT Android con `+` esterno, send interno, mic placeholder rimosso; risposte assistente libere senza vignetta; `Sto pensando` libero/cliccabile con shimmer e reasoning espandibile.
- Android streaming control: durante generazione il bottone invio diventa stop e interrompe job + chiamata OkHttp; testo parziale resta in chat marcato come interrotto.
- Setup locale Hermes Hub: wrapper `hermes-hub.ps1` forza modello LM Studio caricato, `terminal.cwd=C:/Users/Matteo` e Tirith disattivato se non installato per ridurre warning Windows non fatali.

## Release 0.6.9

Hermes Hub 0.6.9 (Windows + Android):

- Header `Sto pensando` con shimmer mostrato sempre durante streaming finché non arriva il primo token di testo, anche quando il server non emette eventi `reasoning`. Si congela in `Pensato per Xs` solo se reasoning è stato ricevuto, altrimenti viene nascosto.
- Rendering markdown del messaggio assistente (testo finale + delta streaming) su Android (`MarkdownText` con `parseMarkdownBlocks` + `renderInlineMarkdown` su `AnnotatedString`) e Windows (`Pages/MarkdownRenderer.cs` con `TextBlock` + `Inlines/Run`). Supporta `# ## ###` headers, `**bold**`, `*italic*` / `_italic_`, `` `inline code` ``, fenced ``` code blocks ``` con language hint, bullet `- ` / `* `.
- Tool call ora dentro `Expander` (Windows) / collapsible `Surface` (Android): header con icona di stato (in corso / riuscito / fallito), nome tool, status; espandendo si vedono `Argomenti` e `Risultato` formattati come JSON pretty-printed (`JsonSerializer.Serialize(WriteIndented=true)` Windows, `org.json.JSONObject/JSONArray.toString(2)` Android) e una riga `Esito: in corso / riuscito / fallito`.
- Inferenza esito tool: `"error"` nel payload del risultato → fallito; status `completato`/`risultato pronto`/`done`/`success` o presenza di `result` → riuscito; altrimenti in corso.

## Release 0.6.8

Hermes Hub 0.6.8 (Windows + Android):

- Stato chat persistente cross-tab: `ChatStateHolder` con `messages`, `draft`, `mode`, `activeConversationId`, `previousResponseId`, `streamingState`, `sending` vive in `ChatApp`; switch verso Archive/Server/Settings/etc non resetta la chat. `chatScope` rememberCoroutineScope sale a `ChatApp` per non interrompere lo streaming durante navigazione.
- Bottone `Nuova` accanto al chip mode (Android + Windows): chiama `resetForNewChat()` Android / svuota `MessagesPanel` + `_messageHistory` su Windows.
- Composer Android non più coperto da tastiera: manifest `android:windowSoftInputMode="adjustResize"`, `WindowCompat.setDecorFitsSystemWindows(window, false)` in onCreate, `Modifier.imePadding()` sulla Column chat + `statusBarsPadding` sulla TopBar.
- Auto-scroll Android: `LazyListState.animateScrollToItem(last)` lanciato in `LaunchedEffect` sui cambiamenti di `messages.size` e `streamingState.text.length` per spostare la chat sempre sull'ultimo messaggio anche durante streaming.
- Windows `HomePage` con `NavigationCacheMode="Required"`: navigando ad altre pagine il Frame riusa la stessa istanza e mantiene history/conversation.

## Release 0.6.7

Hermes Hub 0.6.7 (Windows + Android):

- Streaming SSE token-by-token: client tenta prima `POST /v1/responses` con `stream: true`, fallback `POST /v1/chat/completions` streaming. Parser SSE comune che gestisce eventi Responses (`response.output_text.delta`, `response.reasoning.delta`, `response.output_item.added`, `response.function_call.arguments.delta`, `response.completed`) e formato Chat Completions (`choices[].delta.content`, `delta.reasoning`, `delta.tool_calls`, `usage`).
- Bubble assistente live: header "Sto pensando" con shimmer gradient continuo (LinearGradientBrush animato Windows, `Brush.linearGradient` su `TextStyle` Compose Android). Header cliccabile per espandere il ragionamento (delta di `reasoning`/`reasoning_content`). Dopo il primo token utile lo shimmer si congela in `Pensato per Xs`.
- Tool calls visibili inline: card per ogni tool con nome, status, argomenti streamed e risultato.
- Statistiche risposta: `TTFT / token/s / token / prompt-token / durata` calcolate da `Stopwatch` lato client, usando `usage` server quando disponibile, altrimenti stima `len/4`.
- Composer slash-command: digitando `/` compare popup con lista comandi (`/chat /agente /clear /new /health /server /runs /archive /tasks /settings /about /setup /visual /research /web /image /help`). Navigation tabbed da slash su Android, `Frame.Navigate` su Windows.
- Rimosso vecchio messaggio placeholder `Hermes sta preparando la risposta...` su entrambi i client.
- File `agent.md` (duplicato di `AGENTS.md`) eliminato: era inutile contesto duplicato.

## Stato Attuale

Windows:

- Progetto: `src/NemoclawChat.Windows`
- Stack: WinUI 3, C#, .NET 8, Windows App SDK self-contained.
- Versione app: `0.6.28`.
- Brand/UI: `Hermes Hub`, logo Hermes da `logo hermeshub.png` applicato agli asset Windows e alla UI principale, dark stile ChatGPT, sidebar, composer largo, menu `+`, settings reali.
- UI design system applicato: superfici elevation-aware `#0F1115/#14171D/#1A1E26/#232831`, accent Hermes amber `#F5A524`, hover `#FFC857`, testo muted `#A2ADBF`, bubble utente amber scuro `#7A3E00`, card/composer radius premium e bordi soft.
- Azioni locali: file picker Windows, screen clip, camera URI, nota vocale prompt.
- Chat: invio con Enter, nuova riga con Shift+Enter, chip `Chat`/`Agente` in alto a destra cliccabile, action bubble per menu `+`, scroll automatico, salvataggio cronologia locale.
- Archivio: ricerca locale + dati persistenti, filtri chat/progetti/task/server, riapertura conversazioni, segna progetto, eliminazione elementi salvati con conferma preventiva.
- Recenti sidebar: letti dallo store locale e aggiornati quando cambia archivio; nessun elemento seed finto.
- Chat: prova prima Hermes `POST /v1/responses` con `store`, `conversation` e `previous_response_id`; fallback reale `POST /v1/chat/completions`; fallback locale solo se abilitato.
- Chat/Hermes memory contract: app invia istruzioni e metadata che dichiarano Hermes Hub come client operativo dello stesso Hermes Agent usato dalla CLI. Preferenze stabili, feedback Video/News e regole di lavoro devono usare memoria agente condivisa lato Hermes quando disponibile, non memoria separata solo app. Chat Completions fallback include system message con lo stesso contesto.
- Visual Blocks v1 implementato lato client: chat puo' ricevere `output_text` autosufficiente + `visual_blocks_version: 1` + blocchi tipizzati statici (`markdown`, `code`, `table`, `chart`, `diagram`, `image_gallery`, `callout`). Contratto in `docs/visual-blocks-schema.md` e schema in `config/visual-blocks.schema.json`.
- Jobs: coda persistente su disco, tentativo reale su Hermes Jobs API (`/api/jobs`), `run`/`pause`/`delete` con sync Hermes se disponibile e fallback locale se no.
- Server: dashboard Hermes con `/health`, `/health/detailed`, `/v1/models`, `/v1/capabilities`.
- Runs: pagina dedicata con preset HTTP reali per health, models, capabilities, `POST /v1/runs`, `GET/POST /api/jobs`.
- Vecchio WebSocket operator rimosso dalla UX principale. Servizi legacy restano nel repo non primari.
- Settings: `GatewayUrl` ora significa `Hermes API URL`, default `http://hermes.local:8642/v1`; `GatewayWsUrl` vuoto; `AdminBridgeUrl` derivato root Hermes.
- Credenziale Hermes API key: salvata in Windows Credential Locker; campo vuoto mantiene segreto esistente, reset lo elimina.
- Profilo/About: info app/profilo locale, versione, privacy, gateway attivo.
- Update system: controlla GitHub Releases latest, scarica asset Windows in app con progresso e poi apre installer/asset da bottone `Installa update`.
- Compatibilita storage: usa `%LOCALAPPDATA%\\ChatClaw\\...` ma migra automaticamente da `%LOCALAPPDATA%\\NemoclawChat\\...` se esiste.
- Settings: validazione URL/campi obbligatori, salvataggio locale, reset default, test Hermes `/health`.
- Settings salvate in:

```text
%LOCALAPPDATA%\ChatClaw\settings.json
```

Conversazioni/progetti salvati in:

```text
%LOCALAPPDATA%\ChatClaw\conversations.json
```

Android:

- Progetto: `src/NemoclawChat.Android/app`
- Stack: Kotlin, Jetpack Compose, Gradle.
- Versione app: `0.6.28`, versionCode `41`.
- Brand/UI: `Hermes Hub`, logo Hermes da `logo hermeshub.png` applicato a launcher + UI, bottom nav con icone vere, composer mobile compatto stile ChatGPT Android, menu `+` con Material icons, profilo locale.
- UI design system applicato: superfici elevation-aware `#0F1115/#14171D/#1A1E26/#232831`, accent Hermes amber `#F5A524`, testo muted `#A2ADBF`, bubble utente amber scuro `#7A3E00`, empty state con wash amber e logo grande.
- Azioni locali: file picker Android, camera intent e prompt helper nel menu `+`; dettatura/mic placeholder rimossi finche' non c'e' integrazione reale.
- Chat: action bubble per menu `+`, mode `Chat`/`Agente`, chip mode in alto a destra cliccabile, tentativo Hermes reale (`/v1/responses`, fallback `/v1/chat/completions`), fallback locale esplicito se abilitato, composer stabile compatto a campo singolo/multiriga con `+` esterno e send interno; keyboard handling usa `adjustResize` + `imePadding` solo sul composer, quindi resta sopra la tastiera senza gap inutile; durante generazione il send diventa stop e cancella job + chiamata OkHttp; mic placeholder rimosso; risposte assistente Android libere senza vignetta, thinking cliccabile con shimmer e reasoning espandibile; font globale regolabile da settings con slider continuo e percentuale editabile; sezioni Android rese come righe flat con separatori dritti al posto di card/vignette, salvataggio cronologia locale con `previous_response_id`.
- Android streaming activity: durante generazione mostra una riga shimmer cliccabile con stato live (`Sto processando`, `Sto pensando`, `Sto generando`, `Uso tool: ...`). Espandendo si vedono stato, reasoning ricevuto dal server, tool call, argomenti e risultato; resta visibile anche quando il testo ha gia' iniziato a uscire.
- Android streaming activity UI: header compatto senza frasi tecniche del trasporto; mostra freccia accanto allo stato e percentuale di progresso live. Shimmer deve scorrere da sinistra a destra su tutti gli stati attivi.
- Android streaming latency: il path caldo evita la richiesta `/capabilities` pre-invio, usa loop SSE senza `source.exhausted()` prima di ogni read e mostra feedback immediato quando l'utente preme stop.
- Android `demoMode` default/reset e' `false`: se Hermes non risponde, l'app deve mostrare errore reale invece di mascherare con fallback locale. Fallback solo se utente lo abilita esplicitamente dalle impostazioni.
- Chat/Hermes memory contract: Android invia istruzioni e metadata `memory_policy.scope=shared-hermes-agent-memory`, `share_with_cli=true`, sezioni Hermes Hub e profilo Matteo. Preferenze e feedback devono essere salvati/riusati lato Hermes quando il server espone memoria/tool, non restare solo nello storico app.
- Visual Blocks v1 implementato lato client: stesso contratto Windows, storage retrocompatibile, renderer Compose statico sicuro, nessun HTML/JS/SVG client-side.
- Archivio: tab mobile con ricerca locale persistente, filtri, riapertura conversazioni, salvataggio progetti, contatori, export appunti, rename/delete conversazioni salvate, conferma preventiva prima del delete, icona delete sempre visibile sulla card e azioni che vanno a capo su schermi stretti; nessun seed progetto/chat finto.
- Android UI hardening: righe di azioni in Archivio, Ordini, Server, Aggiornamenti e Settings convertite a layout che va a capo su schermi stretti, per evitare pulsanti nascosti o compressi.
- Jobs: coda task persistente in `SharedPreferences`, creazione job con tentativo reale su Hermes Jobs API, run/pause/delete con sync Hermes se disponibile e fallback locale se no.
- Server: dashboard Hermes/modello/API/sicurezza, test `/health`, lettura reale di `/health/detailed`, `/v1/models`, `/v1/capabilities`.
- Runs: tab dedicata con endpoint manuale e preset reali Hermes per dashboard, modelli, capabilities, runs e jobs. Vecchio WS operator rimosso dalla UX principale.
- Settings: `gatewayWsUrl` vuoto, non mostrato nella UX Hermes.
- Settings: `adminBridgeUrl` derivato da root Hermes, non requisito primario.
- Credenziale Hermes API key: cifrata con Android Keystore AES-GCM e salvata in `chatclaw_settings`; campo vuoto mantiene segreto esistente, reset lo elimina.
- Profilo: info Matteo/app/gateway/privacy/parita Windows.
- Update system: controlla GitHub Releases latest, scarica APK dentro l'app con progress bar + dimensione file e poi apre installer Android con tasto `Aggiorna`.
- Nessun bottone `Release` nella UI update Android: il flusso resta interno all'app come UniNote (`Controlla > Scarica > Aggiorna`).
- Se la versione installata e' gia' l'ultima disponibile: mostra solo stato aggiornato e il controllo refresh, senza bottoni `Scarica`/`Aggiorna`.
- Top bar chat Android: niente label `Demo: ...`; mostra solo brand + chip `Chat/Agente`.
- Icona launcher Android: adaptive icon con foreground ritagliato piu' grande per ridurre il vuoto attorno al logo tra le app.
- Settings: validazione URL/campi obbligatori, salvataggio locale, reset default, test Hermes `/health`.
- Android consente cleartext HTTP verso Hermes locale/Tailscale/LAN tramite `network_security_config`, necessario per `http://<ip-pc>:8642/v1`.
- Android chat HTTP usa timeout lungo per richieste Hermes lente: connect 15s, write 60s, read/call 60 minuti. Serve per modelli LM Studio locali che continuano a generare oltre il timeout breve OkHttp default.
- Setup locale Matteo: `%LOCALAPPDATA%\hermes\.env` contiene default Hermes API Server + LM Studio; comando PATH `hermes-hub.cmd` avvia `hermes-hub.ps1`, legge il modello LLM attualmente caricato da LM Studio via `/api/v1/models` (`loaded_instances`), aggiorna `model.default/provider/base_url` in `%LOCALAPPDATA%\hermes\config.yaml`, forza `terminal.cwd=C:/Users/Matteo`, disattiva Tirith se non installato (`TIRITH_ENABLED=false`/`security.tirith_enabled=false`), poi avvia `hermes gateway run --replace`. Su Ubuntu/Linux usare `scripts/hermes-hub-linux.sh` o il servizio user systemd `scripts/hermes-hub-linux.service` per esporre API su `0.0.0.0:8642` con modello LM Studio caricato. Serve perché Hermes dà precedenza al config model rispetto a `HERMES_INFERENCE_MODEL` e per ridurre warning Windows non fatali.
- Settings salvate in `SharedPreferences` `chatclaw_settings` con migrazione automatica da `nemoclaw_settings`.
- Conversazioni/progetti salvati in `SharedPreferences` `chatclaw_archive` con migrazione automatica da `nemoclaw_archive`.
- Task salvati in `SharedPreferences` `chatclaw_tasks`.
- Progetto Android ora include file root Gradle + wrapper per essere buildabile dal repo.

Documentazione:

- `docs/windows-desktop-guide.md`
- `docs/android-app-guide.md`
- `docs/hermes-hub-conversion.md`
- `docs/visual-blocks-schema.md`
- `config/hermes-defaults.json`
- `config/visual-blocks.schema.json`

Admin Bridge:

- Progetto: `src/ChatClaw.AdminBridge`
- Stack: ASP.NET Core minimal API, .NET 8.
- Auth: bearer token da `CHATCLAW_ADMIN_TOKEN`.
- File root: `CHATCLAW_ADMIN_ROOTS`.
- Audit log: `~/.chatclaw-admin-bridge/audit.log` o `CHATCLAW_ADMIN_AUDIT`.
- Endpoints reali: `/v1/status`, `/v1/actions/{doctor|security-audit|plugin-list}`, `/v1/logs/tail`, `/v1/files/list`, `/v1/files/read`, `/v1/files/write`.

## Preset Hermes

Usare questi default finche' server reale non esiste:

```text
Hermes API URL: http://hermes.local:8642/v1
Health root: http://hermes.local:8642/health
Detailed health: http://hermes.local:8642/health/detailed
Provider: hermes-agent
Endpoint API lato server: http://hermes.local:8642/v1
API primaria: POST /v1/responses
API fallback: POST /v1/chat/completions
Model demo: hermes-agent
Accesso: Tailscale/LAN
Auth: Authorization: Bearer <Hermes API key>
```

Nota architetturale:

- App non devono parlare direttamente con Ollama/local inference.
- App devono parlare a Hermes Agent API Server.
- Chat primaria via Responses API con `store`, `conversation` e `previous_response_id`.
- Runs API e Jobs API sostituiscono la vecchia console operator WS.
- Segreti/provider token restano lato server; la Hermes API key e' l'unico segreto client.
- Visual Blocks v1: `output_text` deve essere completo anche quando ci sono blocchi; history inviata a Hermes deve contenere solo testo umano, non JSON dei blocchi. Client dichiara `metadata.visual_blocks.min_supported_version/max_supported_version/mode`. `mode`: `never` disabilita, `auto` lascia decidere Hermes, `always` preferisce blocchi quando ragionevole senza forzarli.
- Hermes Hub context: ogni richiesta chat/job/run deve dichiarare che l'app ha sezioni Chat, Video, News, Jobs/Runs e Archivio. Video/News devono essere creati lato Hermes/PC come job/artifact con `workspace=video|news`, `stream_url`/`download_url` per video e fonti per news. La memoria agente e' condivisa tra app, CLI e jobs; lo storico locale app non sostituisce la memoria Hermes.
- Sicurezza Visual Blocks: niente HTML, JS o SVG client-side. Diagrammi solo Mermaid source + media proxy pre-renderizzato con fallback code block `mermaid`. Media solo da proxy Hermes, no `file://`, no `data:`, no URL esterni diretti.
- Quicktype Visual Blocks: dopo generazione tipi, controllare discriminator `type` ed enum wire-value C#/Kotlin con `scripts/verify-visual-blocks-contract.ps1`.
- Golden screenshot Visual Blocks: baseline unica versionata in `tests/golden/`, generata da Windows CI; Android confronta con tolleranza e non crea baseline autonoma.

## UI Composer

Direzione richiesta utente:

- Composer deve essere largo come ChatGPT desktop, non stretto.
- Prompt deve occupare la maggior parte dello spazio.
- Finche' testo e' corto: una riga ampia.
- Quando testo va a fine riga: composer cresce in altezza mantenendo larghezza.
- Scelta `Chat`/`Agente` deve stare dentro menu `+`, non occupare spazio principale.
- Menu `+` deve avere simboli/icone per capire subito funzioni.
- Android: bottom nav deve usare icone vere, non lettere placeholder.

## Build

Windows:

```powershell
dotnet build .\src\NemoclawChat.Windows\NemoclawChat.Windows.csproj -c Debug -p:Platform=x64
```

Admin Bridge:

```powershell
dotnet build .\src\ChatClaw.AdminBridge\ChatClaw.AdminBridge.csproj -c Debug
```

Android:

```powershell
$env:ANDROID_HOME='C:\Users\Matteo\AppData\Local\Android\Sdk'
$env:ANDROID_SDK_ROOT=$env:ANDROID_HOME
.\gradlew.bat assembleDebug
```

APK debug:

```text
src/NemoclawChat.Android/app/build/outputs/apk/debug/app-debug.apk
```

Nota update:

- Le app controllano `https://api.github.com/repos/JackoPeru/app-interazione-nemoclaw/releases/latest`.
- Tag release atteso: `vX.Y.Z`, esempio `v0.3.2`.
- Android richiede APK con stesso `applicationId` e stessa firma, `versionCode` maggiore.
- Android debug build non deve usare `applicationIdSuffix`: l'APK generato per release in-app deve restare `com.nemoclaw.chat`.
- Android updater ora supporta download in-app con barra progresso e poi handoff all'installer di sistema.
- Windows ora scarica asset release `.msix`, `.exe` o `.zip` dentro `%LOCALAPPDATA%\ChatClaw\updates\` e poi apre l'installer/asset.

Android SDK locale:

```text
.android-sdk/
```

Questo e' ignorato da Git.

## File Ignorati

Non aggiungere a Git:

```text
.android-sdk/
.tools/
.gradle/
local.properties
bin/
obj/
src/NemoclawChat.Android/app/build/
```

## Prossimi Passi Probabili

- Sezioni Video e News ready to go: aree separate dalla chat con invio reale a Hermes Runs, fallback chat e storico locale separato. Backlog in `prossime implementazioni.md`.
- Migliorare ulteriormente UI desktop/mobile.
- Migliorare UI verticale oltre console unica: pagine separate per approvals realtime, config diff/rollback visuale, file/workspace manager, Admin Bridge, security center avanzato.
- Se arriva backend definitivo, allineare i payload/contratti reali se differiscono da quelli flessibili attuali.
- Prossima milestone: trasformare i probe WS in pagine operative vere: approvazioni exec realtime, modelli/provider, plugin/skills, log/audit, nodi, workspace, config patch, security center.
- Migliorare pairing completo: QR/pairing device e revoca dispositivi. Base credenziali sicure gia' presente: Windows Credential Locker, Android Keystore.
- Collegare evento `exec.approval.requested` e RPC `exec.approval.resolve` quando Gateway reale disponibile.
- Eventuale rebrand tecnico completo dei namespace interni solo se non rompe la compatibilita update Android.
- Aggiungere import archivio locale se richiesto.

## Preferenze Comunicazione

Utente ha attivato `caveman`: risposte brevi, tecniche, senza filler.

Usare italiano.
