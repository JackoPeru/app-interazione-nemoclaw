# Audit round 7 — plan 0.6.26

Stato: comprehensive audit post-0.6.25. Bibbia per agent successivo.

Ogni item: severity, file/line, problema, fix step-by-step con code sample. Agent successivo deve solo applicare.

Workflow attesi:
1. Applica fix uno per uno seguendo l'ordine (Critici → Importanti).
2. Compila dopo ogni gruppo grande.
3. Marca `[x]` quando done.
4. Skip items contrassegnati `[N/A]` o `[DEFER]` con motivo gia' scritto.
5. Bump version 0.6.25 → 0.6.26 a fine.
6. Build assets, commit, tag, push, gh release. Stesso pattern release precedenti.

---

## ANDROID — Critici sicurezza

### [x] **A1** AES-GCM no AAD su gateway secret

- **File**: `src/NemoclawChat.Android/app/src/main/java/com/nemoclaw/chat/MainActivity.kt`
- **Linee**: `saveGatewaySecret` ~4485-4496, `loadGatewaySecret` ~4471-4483, `getOrCreateGatewaySecretKey` ~4528-4548
- **Severity**: CRIT
- **Problema**: Encryption AES-GCM con `cipher.iv` random OK ma manca Authenticated Associated Data (AAD). Attacker con accesso a `SharedPreferences` puo' sostituire ciphertext (es. con secret noto) e GCM verifica solo `MAC(ciphertext+iv+key)` — non lega ad app version o gateway URL. In teoria tampering trasparente.
- **Fix**: aggiungi AAD costante. Esempio:

  ```kotlin
  private val GATEWAY_SECRET_AAD: ByteArray =
      "HermesHub|gateway-secret|v1".toByteArray(Charsets.UTF_8)

  private fun saveGatewaySecret(context: Context, secret: String) {
      if (secret.isBlank()) return
      val cipher = Cipher.getInstance("AES/GCM/NoPadding")
      cipher.init(Cipher.ENCRYPT_MODE, getOrCreateGatewaySecretKey())
      cipher.updateAAD(GATEWAY_SECRET_AAD)   // <-- aggiungi
      val ciphertext = cipher.doFinal(secret.trim().toByteArray(Charsets.UTF_8))
      val packed = cipher.iv + ciphertext
      // ... resto invariato
  }

  private fun loadGatewaySecret(context: Context): String? {
      // ... carica packed come prima
      val cipher = Cipher.getInstance("AES/GCM/NoPadding")
      cipher.init(Cipher.DECRYPT_MODE, key, GCMParameterSpec(128, iv))
      cipher.updateAAD(GATEWAY_SECRET_AAD)   // <-- aggiungi
      String(cipher.doFinal(ciphertext), Charsets.UTF_8)
  }
  ```

- **Note**: AAD deve essere identico ENC/DEC. Se cambi stringa AAD in futuro, secret esistenti diventano non leggibili (forza re-login una tantum).

---

### [x] **A2** Bearer token logged via debug HttpLoggingInterceptor

- **File**: `src/NemoclawChat.Android/app/src/main/java/com/nemoclaw/chat/ChatStream.kt`
- **Linee**: `debugHttpLoggingInterceptor` ~31-44
- **Severity**: HIGH
- **Problema**: `HttpLoggingInterceptor` con `Level.HEADERS` (via reflection) stampa header `Authorization: Bearer xxx...` in logcat su build debug. Qualsiasi app con `READ_LOGS` (pre-Android 4.1) o sviluppatore con adb logcat vede token.
- **Fix**: configura `redactHeader("Authorization")` su interceptor. Usa reflection sullo stesso metodo:

  ```kotlin
  private fun debugHttpLoggingInterceptor(): okhttp3.Interceptor? {
      if (!com.nemoclaw.chat.BuildConfig.DEBUG) return null
      return try {
          val clazz = Class.forName("okhttp3.logging.HttpLoggingInterceptor")
          val instance = clazz.getDeclaredConstructor().newInstance()
          val levelClass = Class.forName("okhttp3.logging.HttpLoggingInterceptor\$Level")
          val headersLevel = levelClass.enumConstants?.firstOrNull { (it as Enum<*>).name == "HEADERS" }
          if (headersLevel != null) {
              clazz.getMethod("setLevel", levelClass).invoke(instance, headersLevel)
          }
          // <-- aggiungi redactHeader
          val redactMethod = clazz.getMethod("redactHeader", String::class.java)
          redactMethod.invoke(instance, "Authorization")
          redactMethod.invoke(instance, "Cookie")
          instance as okhttp3.Interceptor
      } catch (_: Throwable) {
          null
      }
  }
  ```

- **Note**: `redactHeader` esiste in okhttp-logging-interceptor 4+ (presente come `debugImplementation` 5.3.2 in app/build.gradle.kts).

---

### [x] **A3** FileProvider `path="."` espone intera cache + external-files

- **File**: `src/NemoclawChat.Android/app/src/main/res/xml/file_paths.xml`
- **Severity**: HIGH
- **Problema**: `<cache-path name="cache" path="." />` e `<external-files-path name="external_files" path="." />` permettono di condividere qualsiasi file dentro `getCacheDir()` / `getExternalFilesDir()` via URI grants. Se app un domani salva preferenze/temp dentro cache, qualsiasi `Intent` con `FLAG_GRANT_READ_URI_PERMISSION` puo' leggerli.
- **Fix**: restringi a subdirectory specifiche. Crea cartelle dedicate e usale solo per export espliciti:

  ```xml
  <?xml version="1.0" encoding="utf-8"?>
  <paths>
      <cache-path name="exports" path="exports/" />
      <external-files-path name="external_exports" path="exports/" />
  </paths>
  ```

- **Side effect**: tutti i punti che oggi salvano file in `cacheDir.root` per condivisione devono spostarsi in `cacheDir/exports/`. Cerca chiamate a `FileProvider.getUriForFile` in MainActivity.kt e adatta path output.

---

### [x] **A4** RTL override (U+202E) + altri bidi control non bloccati in `makeTitle`

- **File**: `src/NemoclawChat.Android/app/src/main/java/com/nemoclaw/chat/MainActivity.kt`
- **Linee**: `makeTitle` ~4937-4950
- **Severity**: MED
- **Problema**: filter accetta `isLetterOrDigit() || isWhitespace() || ch in "_-...etc"` ma U+202E (RIGHT-TO-LEFT OVERRIDE) e altri bidi control (U+200E, U+200F, U+202A-U+202E, U+2066-U+2069) passano via `isLetterOrDigit` no, ma alcuni passano via `isWhitespace`/symbols. Title spoofing possibile: server invia titolo con override per invertire visualizzazione (es. "Fattura_‮fdp.exe" rendering come "Fattura_exe.pdf").
- **Fix**: blocca esplicitamente bidi control chars:

  ```kotlin
  private val BIDI_CONTROL_CHARS = setOf(
      '‎', '‏',
      '‪', '‫', '‬', '‭', '‮',
      '⁦', '⁧', '⁨', '⁩'
  )

  private fun makeTitle(prompt: String): String {
      val cleaned = prompt
          .replace(' ', ' ')
          .filterNot { it in BIDI_CONTROL_CHARS }   // <-- aggiungi prima del filter
          .lines()
          .joinToString(" ") { it.trim() }
          .filter { ch -> ch.isLetterOrDigit() || ch.isWhitespace() || ch in "_-.,:;?!()[]\"'/\\@#%&*+=" }
          .replace(MULTI_WHITESPACE_REGEX, " ")
          .trim()
      if (cleaned.isEmpty()) return "Nuova richiesta"
      return if (cleaned.length <= 46) cleaned else cleaned.take(46).trimEnd() + "..."
  }
  ```

- **Note**: applica stesso filter anche su `MessageBubble` titolo se mostrato altrove. Cerca render `message.author` / `message.text` su Composable e verifica.

---

## ANDROID — Critici UX/resilience

### [x] **A5** No system back press handler

- **File**: `src/NemoclawChat.Android/app/src/main/java/com/nemoclaw/chat/MainActivity.kt`
- **Linee**: dentro `ChatApp` Composable ~398-...
- **Severity**: HIGH
- **Problema**: tab switch tramite stato `selectedTabName`. Back press di sistema chiude app immediatamente perdendo stato. Utente in Settings preme back → app exit invece di tornare a Chat.
- **Fix**: usa `androidx.activity.compose.BackHandler`. Stack tab history in `rememberSaveable`:

  ```kotlin
  import androidx.activity.compose.BackHandler
  // ...

  @Composable
  private fun ChatApp() {
      // ... esistente
      var tabHistory by rememberSaveable {
          mutableStateOf(listOf(Tab.Chat.name))
      }
      // wrap setSelectedTab
      val setSelectedTab: (Tab) -> Unit = { newTab ->
          if (newTab.name != selectedTabName) {
              tabHistory = (tabHistory + newTab.name).takeLast(10)
              selectedTabName = newTab.name
          }
      }

      // intercetta back
      BackHandler(enabled = tabHistory.size > 1 || sidebarOpen) {
          if (sidebarOpen) {
              sidebarOpen = false
              return@BackHandler
          }
          if (tabHistory.size > 1) {
              val popped = tabHistory.dropLast(1)
              tabHistory = popped
              selectedTabName = popped.last()
          }
      }

      // resto Scaffold ...
  }
  ```

---

### [x] **A6** No offline state detection

- **File**: nuovo + integrazione `ChatScreen` / `Composer`
- **Severity**: HIGH
- **Problema**: app non rileva offline. Utente preme Send, aspetta timeout 60s+ senza feedback. `ConnectivityManager.NetworkCallback` mai usato.
- **Fix**: helper `NetworkStateMonitor` osservato via `produceState`:

  Nuovo file `src/NemoclawChat.Android/app/src/main/java/com/nemoclaw/chat/NetworkState.kt`:
  ```kotlin
  package com.nemoclaw.chat

  import android.content.Context
  import android.net.ConnectivityManager
  import android.net.Network
  import android.net.NetworkRequest
  import androidx.compose.runtime.Composable
  import androidx.compose.runtime.State
  import androidx.compose.runtime.produceState

  @Composable
  fun rememberOnlineState(context: Context): State<Boolean> {
      return produceState(initialValue = true, key1 = context.applicationContext) {
          val cm = context.applicationContext
              .getSystemService(Context.CONNECTIVITY_SERVICE) as ConnectivityManager
          val activeOk = cm.activeNetwork != null
          value = activeOk
          val callback = object : ConnectivityManager.NetworkCallback() {
              override fun onAvailable(network: Network) { value = true }
              override fun onLost(network: Network) { value = cm.activeNetwork != null }
              override fun onUnavailable() { value = false }
          }
          val request = NetworkRequest.Builder()
              .addCapability(android.net.NetworkCapabilities.NET_CAPABILITY_INTERNET)
              .build()
          cm.registerNetworkCallback(request, callback)
          awaitDispose { cm.unregisterNetworkCallback(callback) }
      }
  }
  ```

  Integrazione in `ChatScreen` (sopra Composer):
  ```kotlin
  val online by rememberOnlineState(context)
  if (!online) {
      Surface(color = Color(0xFF7A3E00)) {
          Text(
              modifier = Modifier.fillMaxWidth().padding(8.dp),
              text = "Offline. Hermes non raggiungibile.",
              color = Color.White,
              fontSize = 12.sp
          )
      }
  }
  ```

  Aggiungi permission manifest se non presente:
  ```xml
  <uses-permission android:name="android.permission.ACCESS_NETWORK_STATE" />
  ```

---

### [x] **A7** SSE no reconnect su disconnect

- **File**: `src/NemoclawChat.Android/app/src/main/java/com/nemoclaw/chat/ChatStream.kt`
- **Linee**: `openSseStream` ~263-365, `streamChatRequest` ~167-262
- **Severity**: HIGH
- **Problema**: drop connessione mid-stream = `Done` event mancante, utente vede stream interrotto silenzioso. No retry. No Last-Event-ID resume.
- **Fix**: wrap `openSseStream` in retry loop con backoff. Solo se NON ha ricevuto eventi di tipo "stream completato" (`Done`). Implementazione minima senza Last-Event-ID:

  ```kotlin
  // Dentro streamChatRequest, sostituisci primo blocco openSseStream con loop
  var attempt = 0
  var streamCompletedNormally = false
  while (attempt < 3 && !streamCompletedNormally) {
      attempt++
      val terminated = openSseStream(responseUrl, responsePayload, apiKey, "Hermes Responses API") { ev ->
          if (ev is ChatStreamEvent.Done) streamCompletedNormally = true
          emitAndTrack(ev)
      }
      if (streamCompletedNormally || sawDelta) break
      if (terminated && lastError != null) {
          if (attempt < 3) {
              kotlinx.coroutines.delay(1000L * (1 shl (attempt - 1)))  // 1s, 2s, 4s
              emit(ChatStreamEvent.Status("Riconnessione Hermes (tentativo $attempt)..."))
          }
      }
  }
  ```

- **Note**: cap a 3 retry per non bloccare. Esponi `attempt` count in eventi `Status` per UI.

---

### [x] **A8** No `<queries>` block manifest

- **File**: `src/NemoclawChat.Android/app/src/main/AndroidManifest.xml`
- **Severity**: HIGH
- **Problema**: Android 11+ richiede dichiarazione esplicita pacchetti queryable per `PackageManager.queryIntentActivities`. App invoca `Intent.ACTION_OPEN_DOCUMENT`, `MediaStore.ACTION_IMAGE_CAPTURE` etc. Senza `<queries>`, su Android 12+ alcuni picker possono non risolvere.
- **Fix**: aggiungi prima di `<application>`:

  ```xml
  <queries>
      <intent>
          <action android:name="android.intent.action.OPEN_DOCUMENT" />
          <data android:mimeType="*/*" />
      </intent>
      <intent>
          <action android:name="android.intent.action.GET_CONTENT" />
          <data android:mimeType="*/*" />
      </intent>
      <intent>
          <action android:name="android.media.action.IMAGE_CAPTURE" />
      </intent>
      <intent>
          <action android:name="android.intent.action.VIEW" />
          <data android:scheme="https" />
      </intent>
  </queries>
  ```

---

## ANDROID — Importanti

### [N/A] **A9** Markdown injection in tool result text

- **File**: `src/NemoclawChat.Android/app/src/main/java/com/nemoclaw/chat/ChatStreamUi.kt`
- **Linee**: `ToolActivityRow`/`ToolCallCard` `prettifyJson(tool.result)`
- **Severity**: MED
- **Problema**: result raw passato a markdown renderer indirettamente puo' contenere `[link](http://...)` / `![img](http://...)`. Server Hermes e' trusted ma defense-in-depth: strip markdown da `tool.result` prima di display.
- **Fix**: `tool.result` mostrato come monospace, non markdown. Verifica gia' fatto: ToolActivityRow usa `ActivityLine(... monospaced = true)` che NON applica markdown. Probabilmente OK. Audit: cerca chiamate `MarkdownText(tool.result)`. Se nessuna, mark **[N/A]**. Altrimenti escape:

  ```kotlin
  private fun escapeMarkdownInline(text: String): String =
      text.replace("[", "\\[").replace("]", "\\]")
          .replace("(", "\\(").replace(")", "\\)")
          .replace("*", "\\*").replace("_", "\\_")
          .replace("`", "\\`")
  ```

---

### [x] **A10** `prettifyJson` output unbounded

- **File**: `src/NemoclawChat.Android/app/src/main/java/com/nemoclaw/chat/ChatStreamUi.kt`
- **Linee**: `prettifyJson` ~470-482
- **Severity**: MED
- **Problema**: `JSONObject(trimmed).toString(2)` su 500KB JSON output 1MB+. UI lag su Compose Text.
- **Fix**: cap output:

  ```kotlin
  private const val PRETTIFY_JSON_MAX_CHARS = 20_000

  internal fun prettifyJson(raw: String): String {
      val trimmed = raw.trim()
      if (trimmed.isEmpty()) return raw
      return try {
          val pretty = when {
              trimmed.startsWith("{") -> org.json.JSONObject(trimmed).toString(2)
              trimmed.startsWith("[") -> org.json.JSONArray(trimmed).toString(2)
              else -> raw
          }
          if (pretty.length > PRETTIFY_JSON_MAX_CHARS) {
              pretty.take(PRETTIFY_JSON_MAX_CHARS) + "\n... [troncato a $PRETTIFY_JSON_MAX_CHARS char]"
          } else pretty
      } catch (_: Exception) {
          raw
      }
  }
  ```

---

### [N/A] **A11** Image proxy URL no cache buster

- **File**: `src/NemoclawChat.Android/app/src/main/java/com/nemoclaw/chat/MainActivity.kt`
- **Linee**: `loadRemoteBitmap` ~1335-1380
- **Severity**: LOW
- **Problema**: stesso URL `/v1/media/abc` cached per sempre. Server rotation refresh non riflesso.
- **Fix**: probabilmente OK perche' nessun cache layer attivo (decodeStream diretto). **[N/A]** verifica + skip.

---

### [x] **A12** `previousResponseId` no validation feedback

- **File**: `src/NemoclawChat.Android/app/src/main/java/com/nemoclaw/chat/ChatStream.kt`
- **Linee**: `streamChatRequest` ~210-225
- **Severity**: LOW
- **Problema**: se server respinge `previous_response_id` (400), app fallback a Chat Completions silenzioso. Utente non sa.
- **Fix**: log `Debug.WriteLine` / Android Log + status event:

  ```kotlin
  if (terminated && lastError != null && !sawDelta) {
      android.util.Log.w("ChatStream", "Responses API fallback: $lastError")
      emit(ChatStreamEvent.Status("Responses API non disponibile, fallback rapido a Chat Completions..."))
  }
  ```

---

### [x] **A13** No `SelectionContainer` su MessageBubble

- **File**: `src/NemoclawChat.Android/app/src/main/java/com/nemoclaw/chat/MainActivity.kt`
- **Linee**: `MessageBubble` composable
- **Severity**: MED
- **Problema**: utente non puo' selezionare/copiare testo dei messaggi.
- **Fix**: wrap text in `androidx.compose.foundation.text.selection.SelectionContainer`:

  ```kotlin
  import androidx.compose.foundation.text.selection.SelectionContainer

  @Composable
  private fun MessageBubble(message: ChatMessage) {
      SelectionContainer {
          // contenuto esistente
      }
  }
  ```

---

### [DEFER] **A14** Tablet `WindowSizeClass` breakpoints

- **File**: `src/NemoclawChat.Android/app/src/main/java/com/nemoclaw/chat/MainActivity.kt`
- **Severity**: LOW
- **Problema**: bottom nav sempre, nessun rail nav su tablet/foldable.
- **Fix**: **[DEFER]** richiede dep `material3-window-size-class` + refactor layout. Scope grande, basso impatto LAN home device.

---

### [x] **A15** IME avoidance Settings/Profile screens

- **File**: MainActivity.kt `SettingsScreen`, `ProfileScreen`
- **Severity**: LOW
- **Problema**: tastiera puo' coprire campi su Settings. Composer ha `imePadding()` ma altre screen no.
- **Fix**: add `Modifier.imePadding()` al Column principale di SettingsScreen / ProfileScreen:

  ```kotlin
  LazyColumn(
      modifier = Modifier.imePadding(),
      ...
  )
  ```

---

### [x] **A16** Adaptive icon monochrome layer (Android 13+ themed)

- **File**: `src/NemoclawChat.Android/app/src/main/res/mipmap-anydpi-v26/ic_launcher.xml` (e ic_launcher_round.xml)
- **Severity**: LOW
- **Problema**: senza `<monochrome>` layer, Android 13+ themed icons usano fallback foreground colorato.
- **Fix**: aggiungi drawable monocromatico e referenzia:

  ```xml
  <?xml version="1.0" encoding="utf-8"?>
  <adaptive-icon xmlns:android="http://schemas.android.com/apk/res/android">
      <background android:drawable="@color/ic_launcher_background"/>
      <foreground android:drawable="@drawable/ic_launcher_foreground"/>
      <monochrome android:drawable="@drawable/ic_launcher_monochrome"/>
  </adaptive-icon>
  ```

  Crea `res/drawable/ic_launcher_monochrome.xml` con vector silhouette del logo Hermes (single color).
- **Note**: se non hai vector pulito, **[DEFER]** — bassa priorita'.

---

### [x] **A17** StrictMode in debug

- **File**: nuovo `App` class o `MainActivity.onCreate`
- **Severity**: LOW
- **Problema**: nessun StrictMode = disk/network on main thread non rilevato in dev.
- **Fix**: in `MainActivity.onCreate` prima di setContent:

  ```kotlin
  override fun onCreate(savedInstanceState: Bundle?) {
      if (BuildConfig.DEBUG) {
          StrictMode.setThreadPolicy(StrictMode.ThreadPolicy.Builder()
              .detectAll()
              .penaltyLog()
              .build())
          StrictMode.setVmPolicy(StrictMode.VmPolicy.Builder()
              .detectLeakedClosableObjects()
              .penaltyLog()
              .build())
      }
      super.onCreate(savedInstanceState)
      // resto invariato
  }
  ```

---

### [x] **A18** Lint baseline / strictness

- **File**: `src/NemoclawChat.Android/app/build.gradle.kts`
- **Severity**: LOW
- **Fix**: aggiungi `lint { ... }` block:

  ```kotlin
  android {
      // ...
      lint {
          warningsAsErrors = false
          abortOnError = false
          baseline = file("lint-baseline.xml")
          disable += listOf("MissingTranslation")
      }
  }
  ```

  Poi `./gradlew :app:updateLintBaseline` per generare baseline.

---

### [DEFER] **A19** Scrollbar indicator LazyColumn chat

- **File**: MainActivity.kt `ChatScreen` LazyColumn
- **Severity**: LOW
- **Problema**: nessun indicatore scroll su chat lunghe.
- **Fix**: usa `Modifier.scrollbar` custom o accettare absent (Compose no scrollbar built-in standard). **[DEFER]**.

---

## WINDOWS — Critici

### [x] **W1** MessagesPanel StackPanel non virtualizzato

- **File**: `src/NemoclawChat.Windows/Pages/HomePage.xaml` ~85-90
- **Severity**: CRIT
- **Problema**: `<StackPanel x:Name="MessagesPanel">` renderizza ogni messaggio anche se off-screen. 500+ messaggi = OOM / freeze UI / scroll laggy.
- **Fix**: sostituisci con `ItemsRepeater` + `ItemsRepeaterScrollHost` (WinUI 3 supportato). Refactor:
  1. Crea `ObservableCollection<MessageViewModel>` in HomePage.xaml.cs:
     ```csharp
     public ObservableCollection<MessageViewModel> Messages { get; } = new();
     ```
  2. XAML:
     ```xml
     <ScrollViewer x:Name="MessagesScroll" VerticalScrollMode="Auto">
         <ItemsRepeaterScrollHost>
             <ItemsRepeater ItemsSource="{x:Bind Messages, Mode=OneWay}">
                 <ItemsRepeater.Layout>
                     <StackLayout Orientation="Vertical" Spacing="12" />
                 </ItemsRepeater.Layout>
                 <ItemsRepeater.ItemTemplate>
                     <DataTemplate x:DataType="local:MessageViewModel">
                         <local:MessageBubbleView ViewModel="{x:Bind}" />
                     </DataTemplate>
                 </ItemsRepeater.ItemTemplate>
             </ItemsRepeater>
         </ItemsRepeaterScrollHost>
     </ScrollViewer>
     ```
  3. Tutti i punti che fanno `MessagesPanel.Children.Add(...)` diventano `Messages.Add(new MessageViewModel(...))`.
- **Scope**: refactor medio (~50-100 righe HomePage.xaml.cs). Possibile alternativa minima: `ListView` semplice senza personalizzazione.

---

### [x] **W2** PromptBox no MaxLength

- **File**: `src/NemoclawChat.Windows/Pages/HomePage.xaml`
- **Linee**: TextBox PromptBox
- **Severity**: HIGH
- **Problema**: paste clipboard 100MB = freeze app + memory blow.
- **Fix**: add `MaxLength="50000"`:

  ```xml
  <TextBox x:Name="PromptBox" MaxLength="50000" ... />
  ```

---

### [x] **W3** ChatStream streaming bypass `MaxResponseContentBufferSize`

- **File**: `src/NemoclawChat.Windows/Services/ChatStream.cs`
- **Linee**: ~263 `ReadAsStreamAsync(cancellationToken)`
- **Severity**: HIGH
- **Problema**: `MaxResponseContentBufferSize=10MB` su HttpClient applica solo a `ReadAsStringAsync`. `ReadAsStreamAsync` bypassa. Server malevolo stream gigante = OOM via SSE buffer.
- **Fix**: cap bytes letti nello stream. Wrap `StreamReader` con accumulatore:

  ```csharp
  const long SSE_TOTAL_MAX_BYTES = 50L * 1024 * 1024;  // 50MB hard cap
  long totalRead = 0;

  while (!reader.EndOfStream)
  {
      cancellationToken.ThrowIfCancellationRequested();
      var line = await reader.ReadLineAsync(cancellationToken);
      if (line is null) break;
      totalRead += line.Length;  // approx
      if (totalRead > SSE_TOTAL_MAX_BYTES)
      {
          yield return new StreamError("Stream totale > 50MB, interrotto.");
          yield break;
      }
      // resto invariato ...
  }
  ```

---

### [x] **W4** AdminBridge audit log no rotation

- **File**: `src/ChatClaw.AdminBridge/Program.cs`
- **Linee**: `AuditLog.Write` ~360-395
- **Severity**: HIGH
- **Problema**: append infinito a `audit.log`. Mesi/anni = file GB.
- **Fix**: prima di append check size, rotate se > soglia:

  ```csharp
  sealed class AuditLog(string path)
  {
      private const long MaxBytes = 10L * 1024 * 1024;  // 10MB
      private const int MaxRotations = 5;
      private readonly object _lock = new();

      public void Write(string action, string detail)
      {
          var line = JsonSerializer.Serialize(new
          {
              at = DateTimeOffset.UtcNow,
              action,
              detail
          }) + Environment.NewLine;

          lock (_lock)
          {
              try
              {
                  Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                  RotateIfNeeded();
                  File.AppendAllText(path, line, System.Text.Encoding.UTF8);
              }
              catch (IOException) { }
              catch (UnauthorizedAccessException) { }
          }
      }

      private void RotateIfNeeded()
      {
          try
          {
              var info = new FileInfo(path);
              if (!info.Exists || info.Length < MaxBytes) return;
              // shift .N -> .N+1
              for (int i = MaxRotations - 1; i >= 1; i--)
              {
                  var src = $"{path}.{i}";
                  var dst = $"{path}.{i + 1}";
                  if (File.Exists(src))
                  {
                      if (File.Exists(dst)) File.Delete(dst);
                      File.Move(src, dst);
                  }
              }
              if (File.Exists(path)) File.Move(path, $"{path}.1");
          }
          catch (IOException) { }
      }
  }
  ```

---

### [x] **W5** AdminBridge no rate limiting

- **File**: `src/ChatClaw.AdminBridge/Program.cs`
- **Severity**: HIGH
- **Problema**: brute force token possibile. Senza rate limit, attacker prova 1000 token/sec.
- **Fix**: usa `Microsoft.AspNetCore.RateLimiting` (built-in net 7+):

  ```csharp
  using Microsoft.AspNetCore.RateLimiting;
  using System.Threading.RateLimiting;

  // dopo builder.Services.ConfigureHttpJsonOptions ...
  builder.Services.AddRateLimiter(options =>
  {
      options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpCtx =>
          RateLimitPartition.GetFixedWindowLimiter(
              partitionKey: httpCtx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
              factory: _ => new FixedWindowRateLimiterOptions
              {
                  PermitLimit = 60,
                  Window = TimeSpan.FromMinutes(1),
                  QueueLimit = 0,
                  QueueProcessingOrder = QueueProcessingOrder.OldestFirst
              }));
      options.RejectionStatusCode = 429;
  });

  // dopo app = builder.Build();
  app.UseRateLimiter();
  ```

  Aggiungi `using Microsoft.AspNetCore.Http;` se serve.

---

### [x] **W6** AdminBridge no CORS headers

- **File**: `src/ChatClaw.AdminBridge/Program.cs`
- **Severity**: HIGH
- **Problema**: se client browser/web chiama, requests falliscono CORS. O peggio, default permissive da reverse proxy.
- **Fix**: policy deny esplicita o whitelist limitata:

  ```csharp
  builder.Services.AddCors(options =>
  {
      options.AddPolicy("default", policy =>
      {
          policy.WithOrigins("https://hermes.local", "http://hermes.local")
                .AllowAnyHeader()
                .AllowAnyMethod();
      });
  });

  app.UseCors("default");
  ```

---

### [x] **W7** MarkdownRenderer no output cap

- **File**: `src/NemoclawChat.Windows/Pages/MarkdownRenderer.cs`
- **Severity**: HIGH
- **Problema**: server invia 10k righe = 10k UI children. Freeze.
- **Fix**: cap blocks render:

  ```csharp
  private const int MAX_BLOCKS = 500;
  private const int MAX_INPUT_CHARS = 200_000;

  public static UIElement Render(string markdown, ...)
  {
      var safe = markdown.Length > MAX_INPUT_CHARS
          ? markdown.Substring(0, MAX_INPUT_CHARS)
          : markdown;
      var blocks = ParseBlocks(safe).Take(MAX_BLOCKS).ToList();
      // ...
  }
  ```

  Cerca classe/funzione e ispeziona prima.

---

### [x] **W8** VisualBlocks JsonSerializer no `MaxDepth`

- **File**: `src/NemoclawChat.Windows/Services/VisualBlocks.cs` (o file con `JsonSerializer.Deserialize<VisualBlockRecord>`)
- **Severity**: HIGH
- **Problema**: deep nesting attack = StackOverflow.
- **Fix**: usa `JsonSerializerOptions` con `MaxDepth = 16`:

  ```csharp
  private static readonly JsonSerializerOptions ParseOptions = new()
  {
      MaxDepth = 16,
      PropertyNamingPolicy = JsonNamingPolicy.CamelCase
  };

  // usa ParseOptions in tutti i Deserialize
  JsonSerializer.Deserialize<VisualBlockRecord>(json, ParseOptions);
  ```

---

### [x] **W9** No user error feedback (InfoBar/Toast)

- **File**: `src/NemoclawChat.Windows/Pages/HomePage.xaml` + HomePage.xaml.cs
- **Severity**: HIGH
- **Problema**: Send fail = silenzio. Utente non sa errore stream.
- **Fix**: aggiungi `<InfoBar>` top di Page, esponi metodo `ShowError(string)`:

  ```xml
  <InfoBar x:Name="ErrorInfoBar"
           Severity="Error"
           IsOpen="False"
           IsClosable="True"
           Margin="12,4" />
  ```

  ```csharp
  private void ShowError(string message)
  {
      ErrorInfoBar.Title = "Errore";
      ErrorInfoBar.Message = message;
      ErrorInfoBar.Severity = InfoBarSeverity.Error;
      ErrorInfoBar.IsOpen = true;
  }

  // chiama da catch in SendCurrentPromptAsync
  catch (Exception ex)
  {
      ShowError($"Stream interrotto: {ex.Message}");
      bubble?.AppendText($"\n[errore stream] {ex.Message}");
      // ...
  }
  ```

---

## WINDOWS — Importanti

### [x] **W10** Right-click context menu chat messages

- **File**: HomePage.xaml + bubble template
- **Severity**: MED
- **Fix**: add `<MenuFlyout>` `ContextFlyout` su TextBlock messaggi:

  ```xml
  <TextBlock x:Name="Body">
      <TextBlock.ContextFlyout>
          <MenuFlyout>
              <MenuFlyoutItem Text="Copia" Click="CopyMessage_Click"/>
          </MenuFlyout>
      </TextBlock.ContextFlyout>
  </TextBlock>
  ```

  Handler:
  ```csharp
  private void CopyMessage_Click(object sender, RoutedEventArgs e)
  {
      var item = (sender as MenuFlyoutItem)?.DataContext as string;
      if (item is null) return;
      var dp = new DataPackage();
      dp.SetText(item);
      Clipboard.SetContent(dp);
  }
  ```

---

### [x] **W11** Spell check PromptBox

- **File**: HomePage.xaml PromptBox TextBox
- **Severity**: LOW
- **Fix**: `IsSpellCheckEnabled="True"`:

  ```xml
  <TextBox x:Name="PromptBox" IsSpellCheckEnabled="True" MaxLength="50000" ... />
  ```

---

### [x] **W12** Narrator AutomationProperties incompleti

- **File**: HomePage.xaml + MainWindow.xaml
- **Severity**: MED
- **Problema**: PromptBox, SendButton, ecc. senza `AutomationProperties.Name`.
- **Fix**: aggiungi su ogni interactive:

  ```xml
  <TextBox x:Name="PromptBox" AutomationProperties.Name="Prompt input" ... />
  <Button x:Name="SendButton" AutomationProperties.Name="Invia messaggio" ... />
  <Button x:Name="NewChatButton" AutomationProperties.Name="Nuova chat" ... />
  ```

  Pattern: ogni Button/TextBox/icona deve avere `Name` o `LabeledBy`.

---

### [x] **W13** Keyboard shortcuts Ctrl+N, Ctrl+L

- **File**: HomePage.xaml.cs
- **Severity**: MED
- **Fix**: add `KeyboardAccelerator`:

  ```xml
  <Page.KeyboardAccelerators>
      <KeyboardAccelerator Modifiers="Control" Key="N" Invoked="NewChat_Invoked"/>
      <KeyboardAccelerator Modifiers="Control" Key="L" Invoked="ClearChat_Invoked"/>
  </Page.KeyboardAccelerators>
  ```

  Handler:
  ```csharp
  private void NewChat_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
  {
      // delega a MainWindow new chat
      args.Handled = true;
      Frame.Navigate(typeof(HomePage), new HomeNavigationRequest());
  }
  ```

---

### [x] **W14** Window state position/size persistence

- **File**: `src/NemoclawChat.Windows/MainWindow.xaml.cs`
- **Severity**: LOW
- **Fix**: salva PositionInt32 + SizeInt32 in AppSettings o file dedicato. Carica su `Activated`:

  ```csharp
  private void SaveWindowState()
  {
      var prefs = ApplicationData.Current.LocalSettings.Values;
      var presenter = AppWindow.Presenter as OverlappedPresenter;
      prefs["WindowX"] = AppWindow.Position.X;
      prefs["WindowY"] = AppWindow.Position.Y;
      prefs["WindowW"] = AppWindow.Size.Width;
      prefs["WindowH"] = AppWindow.Size.Height;
  }

  private void RestoreWindowState()
  {
      var prefs = ApplicationData.Current.LocalSettings.Values;
      if (prefs.TryGetValue("WindowW", out var w) && w is int width && width > 200)
      {
          AppWindow.Resize(new SizeInt32(width, (int)prefs["WindowH"]));
          AppWindow.Move(new PointInt32((int)prefs["WindowX"], (int)prefs["WindowY"]));
      }
  }
  ```

  Chiama `RestoreWindowState()` in constructor (dopo `AppWindow.SetIcon`) e `SaveWindowState()` in `MainWindow_Closed`.

---

### [x] **W15** Application RequestedTheme esplicito

- **File**: `src/NemoclawChat.Windows/App.xaml`
- **Severity**: LOW
- **Fix**: top di Application root:

  ```xml
  <Application
      ...
      RequestedTheme="Dark">
  ```

---

### [x] **W16** Drag-drop file su PromptBox

- **File**: HomePage.xaml + HomePage.xaml.cs
- **Severity**: MED
- **Fix**: `AllowDrop="True"` + handlers:

  ```xml
  <TextBox x:Name="PromptBox"
           AllowDrop="True"
           DragOver="PromptBox_DragOver"
           Drop="PromptBox_Drop"
           ... />
  ```

  ```csharp
  private void PromptBox_DragOver(object sender, DragEventArgs e)
  {
      if (e.DataView.Contains(StandardDataFormats.StorageItems))
      {
          e.AcceptedOperation = DataPackageOperation.Copy;
      }
  }

  private async void PromptBox_Drop(object sender, DragEventArgs e)
  {
      if (!e.DataView.Contains(StandardDataFormats.StorageItems)) return;
      var items = await e.DataView.GetStorageItemsAsync();
      foreach (var item in items.Take(5))
      {
          if (item is StorageFile file)
          {
              PromptBox.Text += $"\nFile allegato: {file.Path}";
          }
      }
  }
  ```

---

### [x] **W17** Clipboard paste image

- **File**: HomePage.xaml.cs
- **Severity**: MED
- **Fix**: intercept Ctrl+V su PromptBox, check `Clipboard.GetContent().Contains(StandardDataFormats.Bitmap)`:

  ```csharp
  private async void PromptBox_Paste(object sender, TextControlPasteEventArgs e)
  {
      var view = Clipboard.GetContent();
      if (view.Contains(StandardDataFormats.Bitmap))
      {
          e.Handled = true;
          // notifica utente che paste immagine non supportato o salva temp + attach
          ShowError("Paste immagine non ancora supportato. Salva file e usa allega.");
      }
  }
  ```

---

### [x] **W18** AdminBridge graceful shutdown Ctrl+C

- **File**: `src/ChatClaw.AdminBridge/Program.cs`
- **Severity**: LOW
- **Fix**: hookup `IHostApplicationLifetime`:

  ```csharp
  var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
  lifetime.ApplicationStopping.Register(() =>
  {
      Console.WriteLine("[admin-bridge] shutdown gracefully...");
      // flush audit log gia' sync, niente da fare di critico
  });
  ```

---

### [x] **W19** AdminBridge config reload endpoint

- **File**: `src/ChatClaw.AdminBridge/Program.cs`
- **Severity**: LOW
- **Fix**: aggiungi endpoint POST `/v1/reload` che richiama `BridgeConfig.Load()` e atomicamente rimpiazza `config`. Richiede refactor `config` da `var` a campo statico thread-safe. **[DEFER se troppo invasivo]**.

---

### [DEFER] **W20** WindowsAppSDK version update check

- **File**: `src/NemoclawChat.Windows/NemoclawChat.Windows.csproj`
- **Severity**: LOW
- **Fix**: aggiorna PackageReference `Microsoft.WindowsAppSDK` a versione corrente. Verifica breaking changes release notes. **[DEFER se test prevedono regression]**.

---

### [x] **W21** AppSettingsStore migration race

- **File**: `src/NemoclawChat.Windows/Services/AppSettingsStore.cs`
- **Severity**: LOW (single-user)
- **Fix**: wrap migration block in try/catch tollerante `IOException`:

  ```csharp
  private static string DataDirectoryPath
  {
      get
      {
          var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
          var currentDirectory = Path.Combine(localAppData, CurrentDirectoryName);
          var legacyDirectory = Path.Combine(localAppData, LegacyDirectoryName);

          try
          {
              if (!Directory.Exists(currentDirectory) && Directory.Exists(legacyDirectory))
              {
                  Directory.CreateDirectory(currentDirectory);
                  foreach (var file in Directory.GetFiles(legacyDirectory))
                  {
                      var destination = Path.Combine(currentDirectory, Path.GetFileName(file));
                      if (!File.Exists(destination))
                      {
                          try { File.Copy(file, destination); } catch (IOException) { }
                      }
                  }
              }
              Directory.CreateDirectory(currentDirectory);
          }
          catch (IOException) { /* ignore, ritorna path comunque */ }
          return currentDirectory;
      }
  }
  ```

---

### [x] **W22** Logging persistente release

- **File**: `src/NemoclawChat.Windows/App.xaml.cs` + new `Services/FileLogger.cs`
- **Severity**: MED
- **Problema**: `Debug.WriteLine` non visibile in Release. Troubleshooting impossibile.
- **Fix**: crea logger semplice scrivendo a `%LOCALAPPDATA%\ChatClaw\logs\app.log` con rotation 5 file da 2MB. Re-instrada `Debug.WriteLine` via `Trace.Listeners.Add(new TextWriterTraceListener(...))`.

  ```csharp
  // App.xaml.cs constructor
  var logPath = Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
      "ChatClaw", "logs", "app.log");
  Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
  if (new FileInfo(logPath).Exists && new FileInfo(logPath).Length > 2 * 1024 * 1024)
  {
      // simple rotate
      File.Move(logPath, logPath + "." + DateTimeOffset.UtcNow.ToUnixTimeSeconds(), overwrite: true);
  }
  var writer = new StreamWriter(File.Open(logPath, FileMode.Append, FileAccess.Write, FileShare.Read))
  {
      AutoFlush = true
  };
  System.Diagnostics.Trace.Listeners.Add(new System.Diagnostics.TextWriterTraceListener(writer));
  System.Diagnostics.Debug.AutoFlush = true;
  System.Diagnostics.Trace.AutoFlush = true;
  ```

---

### [x] **W23** Sidebar buttons tooltips parziali

- **File**: `src/NemoclawChat.Windows/MainWindow.xaml`
- **Severity**: LOW
- **Fix**: aggiungi `ToolTipService.ToolTip="..."` su ogni button sidebar (Archive, Tasks, Server, Operator, Video, News, Settings, About).

---

### [DEFER] **W24** Touch/pen input handlers

- **Severity**: LOW
- **Fix**: **[DEFER]** — WinUI 3 gestisce pointer input automaticamente via Click/Tapped. Test su touch device prima di refactor.

---

### [x] **W25** High contrast mode check

- **File**: `src/NemoclawChat.Windows/App.xaml.cs`
- **Severity**: LOW
- **Fix**: query `AccessibilitySettings.HighContrast`, swap resource brushes se true:

  ```csharp
  var settings = new Windows.UI.ViewManagement.AccessibilitySettings();
  if (settings.HighContrast)
  {
      Application.Current.Resources["MutedTextBrush"] =
          new SolidColorBrush(Microsoft.UI.Colors.White);
  }
  ```

---

## Esclusi (motivo)

- **i18n strings.xml Android**: 300+ stringhe scattered, refactor enorme.
- **Light theme Android**: refactor `ChatClawTheme` con dynamic schema.
- **HomePage 1100 / MainActivity 5000+ righe monolith refactor**: scope troppo grande per single release. Defer a release dedicata di refactor.
- **Signing keystore release dedicato Android**: debug keystore intenzionale per in-app updates (richiede keystore persistente fuori repo + decision strategy).
- **AppUpdateService resume download**: richiede Range header + protocol design.
- **AdminBridge OpenAPI/Swagger**: utile ma non critico.
- **AdminBridge test project**: defer, no test infra esistente.
- **NET 8 → 9 migration**: NET 8 LTS fino Nov 2026, ok.
- **Mica/Acrylic backdrop Windows**: cosmetic.
- **Crashlytics/telemetry integration**: richiede provider scelto (Sentry/AppCenter).
- **Push notifications Android, Foreground service**: app non ne ha bisogno.
- **Hermes Hub instructions i18n**: hardcoded IT, scope grande.

---

## Procedure release 0.6.26

1. Applica tutti i Critici e Importanti non `[DEFER]`/`[N/A]`.
2. Marca ogni item `[x]` con nota breve fix applicato.
3. Compile checks (in quest'ordine):
   ```bash
   cd "src/NemoclawChat.Android" && export ANDROID_HOME="C:/Users/Matteo/AppData/Local/Android/Sdk" && ./gradlew :app:compileDebugKotlin
   dotnet build src/ChatClaw.AdminBridge/ChatClaw.AdminBridge.csproj --nologo
   dotnet publish src/NemoclawChat.Windows/NemoclawChat.Windows.csproj -c Release -p:PublishProfile=win-x64 --nologo
   ```
4. Fix qualsiasi errore compile prima di proseguire.
5. Bump version 0.6.25 → 0.6.26 in:
   - `src/NemoclawChat.Android/app/build.gradle.kts` — `versionCode = 39`, `versionName = "0.6.26"`
   - `src/ChatClaw.AdminBridge/ChatClaw.AdminBridge.csproj` — `<Version>0.6.26</Version>`
   - `src/NemoclawChat.Windows/NemoclawChat.Windows.csproj` — `<Version>0.6.26</Version>` + AssemblyVersion + FileVersion
   - `src/NemoclawChat.Windows/Package.appxmanifest` — `Version="0.6.26.0"`
   - `AGENTS.md` — `v0.6.26 Release Hermes Hub 0.6.26` su line 33-37, "Versione app: `0.6.26`" su Windows section, "Versione app: `0.6.26`, versionCode `39`" su Android section, e nuova sezione "## Release Corrente" con sommario fix applicati.
6. Build assets:
   - APK: `./gradlew :app:assembleDebug` → `src/NemoclawChat.Android/app/build/outputs/apk/debug/app-debug.apk`
   - Windows publish: come sopra
   - AdminBridge publish: `dotnet publish src/ChatClaw.AdminBridge/ChatClaw.AdminBridge.csproj -c Release -r win-x64 --self-contained true --nologo`
7. Stage assets via PowerShell ZipFile.CreateFromDirectory in `release-assets/v0.6.26/`:
   - `HermesHub-0.6.26-android.apk`
   - `HermesHub-0.6.26-windows-x64.zip`
   - `HermesHub-0.6.26-admin-bridge-win-x64.zip`
   - `HermesHub-0.6.26-linux-helper.zip` (zip scripts/ dir)
8. `git add AGENTS.md docs src && git commit -m "Release Hermes Hub 0.6.26" && git tag -a v0.6.26 -m "Release Hermes Hub 0.6.26" && git push origin main && git push origin v0.6.26`
9. `gh release create v0.6.26 --title "..." --notes "..." <asset paths>`
10. Verifica `curl -s https://api.github.com/repos/JackoPeru/app-interazione-nemoclaw/releases/latest` → `tag_name: v0.6.26`.

---

## Note operative finali

- Ogni fix sopra contiene **file path completo + linee approssimate + sample code**. Agent successivo deve aprire file, verificare context, applicare edit.
- Se context non matcha esattamente (lint format/whitespace), ri-leggere con Read tool + applicare Edit con string esatto.
- Se item `[N/A]` dopo lettura (gia' fatto), marca con motivo "verificato gia' fixed in <release>".
- Se item `[DEFER]`, marca con motivo "deferred: <scope reason>".
- Mantieni stile commit caveman come release precedenti: titolo "Release Hermes Hub 0.6.26", body bullet con tag categoria.
- AGENTS.md "Release Corrente" sezione completa: lista tutti i fix applicati raggruppati Critici/Importanti, riferimento `docs/audit-0.6.26.md`.
- Spostare release precedente da "Release Corrente" a "Release 0.6.25" header.

Lavoro stimato: ~3-4h se agent procede ordinato. Punti piu' lunghi:
- W1 (MessagesPanel virtualization) — refactor ~60-100 righe XAML + cs
- W22 (FileLogger) — nuovo service ~30 righe
- A5 + A6 (BackHandler + offline) — refactor moderato in ChatApp Composable
- A7 (SSE reconnect) — modifica streamChatRequest

Tutti gli altri sono one-liner o ~5-10 righe.
