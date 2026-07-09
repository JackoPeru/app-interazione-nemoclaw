package com.nemoclaw.chat

import android.Manifest
import android.app.Activity
import android.content.Context
import android.content.pm.PackageManager
import android.graphics.Color as AndroidColor
import android.media.MediaRecorder
import android.os.Build
import android.util.Log
import android.view.View
import android.webkit.ConsoleMessage
import android.webkit.WebChromeClient
import android.webkit.WebResourceRequest
import android.webkit.WebSettings
import android.webkit.WebView
import android.webkit.WebViewClient
import android.widget.Toast
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.rounded.Mic
import androidx.compose.material.icons.rounded.Stop
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.Icon
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateListOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalView
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.viewinterop.AndroidView
import androidx.core.content.ContextCompat
import androidx.core.view.WindowInsetsCompat
import androidx.core.view.WindowInsetsControllerCompat
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.LifecycleEventObserver
import androidx.lifecycle.compose.LocalLifecycleOwner
import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.collect
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.MultipartBody
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.asRequestBody
import org.json.JSONObject
import java.io.File
import java.net.URI
import java.util.concurrent.TimeUnit
import kotlin.coroutines.coroutineContext

private const val VoiceSceneUrl = "file:///android_asset/hermes_scene/orange_particles_3d.html"

private val voiceHttpClient: OkHttpClient by lazy {
    OkHttpClient.Builder()
        .connectTimeout(10, TimeUnit.SECONDS)
        .readTimeout(90, TimeUnit.SECONDS)
        .writeTimeout(60, TimeUnit.SECONDS)
        .callTimeout(120, TimeUnit.SECONDS)
        .retryOnConnectionFailure(true)
        .build()
}

@Composable
internal fun VoiceModeScreen(settings: AppSettings, apiKey: String?) {
    val context = LocalContext.current
    val view = LocalView.current
    val scope = rememberCoroutineScope()
    val history = remember { mutableStateListOf<ChatMessage>() }
    var webViewRef by remember { mutableStateOf<WebView?>(null) }
    var callActive by remember { mutableStateOf(false) }
    var isBusy by remember { mutableStateOf(false) }
    var isSpeaking by remember { mutableStateOf(false) }
    var status by remember { mutableStateOf("Premi per avviare la chiamata.") }
    var callJob by remember { mutableStateOf<Job?>(null) }

    val permissionLauncher = rememberLauncherForActivityResult(ActivityResultContracts.RequestPermission()) { granted ->
        if (!granted) Toast.makeText(context, "Permesso microfono negato.", Toast.LENGTH_SHORT).show()
    }

    DisposableEffect(view) {
        val activity = context as? Activity
        val controller = activity?.let { WindowInsetsControllerCompat(it.window, view) }
        controller?.systemBarsBehavior = WindowInsetsControllerCompat.BEHAVIOR_SHOW_TRANSIENT_BARS_BY_SWIPE
        controller?.hide(WindowInsetsCompat.Type.systemBars())
        onDispose {
            controller?.show(WindowInsetsCompat.Type.systemBars())
        }
    }

    DisposableEffect(Unit) {
        onDispose {
            callJob?.cancel()
            webViewRef?.stopLoading()
            webViewRef?.destroy()
            webViewRef = null
        }
    }

    LaunchedEffect(isSpeaking) {
        webViewRef?.evaluateJavascript("window.setHermesSpeaking(${if (isSpeaking) "true" else "false"})", null)
    }

    val lifecycleOwner = LocalLifecycleOwner.current
    DisposableEffect(lifecycleOwner) {
        val observer = LifecycleEventObserver { _, event ->
            if (event == Lifecycle.Event.ON_PAUSE) {
                webViewRef?.onPause()
            } else if (event == Lifecycle.Event.ON_RESUME) {
                webViewRef?.onResume()
            }
        }
        lifecycleOwner.lifecycle.addObserver(observer)
        onDispose {
            lifecycleOwner.lifecycle.removeObserver(observer)
        }
    }

    Box(Modifier.fillMaxSize().background(Color.Black)) {
        AndroidView(
            modifier = Modifier.fillMaxSize(),
            factory = { ctx ->
                WebView(ctx).apply {
                    webViewRef = this
                    setBackgroundColor(AndroidColor.BLACK)
                    setLayerType(View.LAYER_TYPE_HARDWARE, null)
                    isVerticalScrollBarEnabled = false
                    isHorizontalScrollBarEnabled = false
                    overScrollMode = View.OVER_SCROLL_NEVER
                    webViewClient = object : WebViewClient() {
                        override fun shouldOverrideUrlLoading(view: WebView, request: WebResourceRequest): Boolean {
                            return request.url.toString() != VoiceSceneUrl
                        }
                    }
                    webChromeClient = object : WebChromeClient() {
                        override fun onConsoleMessage(consoleMessage: ConsoleMessage): Boolean {
                            if (BuildConfig.DEBUG) {
                                Log.d("HermesVoiceWebView", "${consoleMessage.messageLevel()}: ${consoleMessage.message()}")
                            }
                            return true
                        }
                    }
                    this.settings.javaScriptEnabled = true
                    this.settings.domStorageEnabled = true
                    @Suppress("DEPRECATION")
                    this.settings.databaseEnabled = true
                    this.settings.cacheMode = WebSettings.LOAD_NO_CACHE
                    this.settings.allowFileAccess = true
                    this.settings.allowContentAccess = false
                    @Suppress("DEPRECATION")
                    this.settings.allowFileAccessFromFileURLs = true
                    @Suppress("DEPRECATION")
                    this.settings.allowUniversalAccessFromFileURLs = false
                    this.settings.mediaPlaybackRequiresUserGesture = false
                    loadUrl(VoiceSceneUrl)
                }
            },
            update = { webView ->
                if (webView.url == null) webView.loadUrl(VoiceSceneUrl)
            }
        )

        Column(
            modifier = Modifier
                .align(Alignment.BottomCenter)
                .padding(bottom = 34.dp, start = 24.dp, end = 24.dp),
            horizontalAlignment = Alignment.CenterHorizontally,
            verticalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            Button(
                onClick = {
                    if (!callActive) {
                        if (ContextCompat.checkSelfPermission(context, Manifest.permission.RECORD_AUDIO) != PackageManager.PERMISSION_GRANTED) {
                            permissionLauncher.launch(Manifest.permission.RECORD_AUDIO)
                            return@Button
                        }
                        callActive = true
                        isSpeaking = false
                        status = "Chiamata attiva. Parla quando vuoi."
                        callJob = scope.launch {
                            runVoiceCallLoop(
                                context = context,
                                settings = settings,
                                apiKey = apiKey,
                                history = history,
                                scope = scope,
                                isCallActive = { callActive },
                                isBusy = { isBusy || isSpeaking },
                                setBusy = { isBusy = it },
                                setStatus = { status = it },
                                setSpeaking = { isSpeaking = it }
                            )
                        }
                    } else {
                        callActive = false
                        callJob?.cancel()
                        callJob = null
                        isBusy = false
                        isSpeaking = false
                        status = "Chiamata chiusa."
                    }
                },
                shape = CircleShape,
                colors = ButtonDefaults.buttonColors(
                    containerColor = if (callActive) Color(0xFF922323) else Color(0x33222222),
                    disabledContainerColor = Color(0x22333333)
                ),
                modifier = Modifier
                    .size(78.dp)
                    .clip(CircleShape)
                    .border(1.dp, Color(0x66FFFFFF), CircleShape)
            ) {
                Icon(if (callActive) Icons.Rounded.Stop else Icons.Rounded.Mic, contentDescription = "Voce", tint = Color.White)
            }
            Text(
                text = status,
                color = Color(0xCCFFFFFF),
                textAlign = TextAlign.Center,
                maxLines = 2,
                overflow = TextOverflow.Ellipsis
            )
        }
    }
}

private suspend fun runVoiceCallLoop(
    context: Context,
    settings: AppSettings,
    apiKey: String?,
    history: MutableList<ChatMessage>,
    scope: CoroutineScope,
    isCallActive: () -> Boolean,
    isBusy: () -> Boolean,
    setBusy: (Boolean) -> Unit,
    setStatus: (String) -> Unit,
    setSpeaking: (Boolean) -> Unit
) {
    var lastTranscript = ""
    var lastTranscriptAt = 0L
    while (coroutineContext.isActive && isCallActive()) {
        try {
            if (isBusy()) {
                delay(180)
                continue
            }
            setStatus("Ascolto...")
            val file = captureVoiceChunk(context)
            if (file == null) {
                delay(120)
                continue
            }
            setBusy(true)
            setStatus("Trascrivo...")
            val text = transcribeVoiceFile(settings, apiKey, file).trim()
            file.delete()
            val now = System.currentTimeMillis()
            if (text.length >= 2 && !(text.equals(lastTranscript, ignoreCase = true) && now - lastTranscriptAt < 8_000)) {
                lastTranscript = text
                lastTranscriptAt = now
                setStatus("Tu: ${text.trimForStatus()}")
                runVoiceTurn(context, settings, apiKey, history, text, scope, setStatus, setSpeaking)
            } else {
                setStatus("Ascolto...")
            }
        } catch (_: CancellationException) {
            break
        } catch (ex: Exception) {
            setStatus("Errore voce: ${ex.message}")
            delay(900)
        } finally {
            setBusy(false)
        }
    }
}

private suspend fun captureVoiceChunk(
    context: Context
): File? = withContext(Dispatchers.IO) {
    val file = File(context.cacheDir, "voice-call-${System.currentTimeMillis()}.m4a")
    val recorder = createVoiceRecorder(context, file)
    var heardVoice = false
    var lastVoiceAt = 0L
    val startedAt = System.currentTimeMillis()
    try {
        recorder.prepare()
        recorder.start()
        while (coroutineContext.isActive) {
            delay(120)
            val now = System.currentTimeMillis()
            val amplitude = runCatching { recorder.maxAmplitude }.getOrDefault(0)
            if (amplitude > 1_400) {
                heardVoice = true
                lastVoiceAt = now
            }
            if (heardVoice && now - lastVoiceAt > 760 && now - startedAt > 900) break
            if (now - startedAt > 3_600) break
        }
    } finally {
        recorder.runCatchingStopAndRelease()
    }
    if (heardVoice && file.length() > 900) file else {
        file.delete()
        null
    }
}

private suspend fun runVoiceTurn(
    context: Context,
    settings: AppSettings,
    apiKey: String?,
    history: MutableList<ChatMessage>,
    prompt: String,
    scope: CoroutineScope,
    setStatus: (String) -> Unit,
    setSpeaking: (Boolean) -> Unit
) {
    history += ChatMessage("Tu", prompt, fromUser = true)
    val answer = StringBuilder()
    val speechBuffer = StringBuilder()
    var playback = CompletableDeferred(Unit).also { it.complete(Unit) }
    setStatus("Hermes sta rispondendo...")
    streamChatRequest(
        settings = settings,
        mode = "Chat",
        prompt = "Rispondi in italiano in modo naturale e conversazionale. Utente in chiamata vocale: $prompt",
        history = history.takeLast(20),
        conversationId = null,
        previousResponseId = null,
        attachments = emptyList(),
        apiKey = apiKey
    ).collect { event ->
        when (event) {
            is ChatStreamEvent.TextDelta -> {
                answer.append(event.delta)
                speechBuffer.append(event.delta)
                setStatus("Hermes: ${answer.toString().trimForStatus()}")
                playback = queueVoiceSpeech(context, settings, apiKey, speechBuffer, flush = false, previous = playback, scope = scope, setSpeaking = setSpeaking)
            }
            is ChatStreamEvent.Error -> setStatus("Hermes: ${event.message}")
            else -> Unit
        }
    }
    playback = queueVoiceSpeech(context, settings, apiKey, speechBuffer, flush = true, previous = playback, scope = scope, setSpeaking = setSpeaking)
    playback.await()
    val finalText = answer.toString().trim()
    if (finalText.isNotBlank()) history += ChatMessage("Hermes", finalText, fromUser = false)
    setSpeaking(false)
    setStatus("Ascolto...")
}

private fun queueVoiceSpeech(
    context: Context,
    settings: AppSettings,
    apiKey: String?,
    buffer: StringBuilder,
    flush: Boolean,
    previous: CompletableDeferred<Unit>,
    scope: CoroutineScope,
    setSpeaking: (Boolean) -> Unit
): CompletableDeferred<Unit> {
    var chain = previous
    while (true) {
        val cut = findSpeechCut(buffer.toString(), flush)
        if (cut <= 0) return chain
        val chunk = buffer.substring(0, cut).trim()
        buffer.delete(0, cut)
        if (chunk.length < 2) continue
        val next = CompletableDeferred<Unit>()
        val waitFor = chain
        scope.launch {
            waitFor.await()
            try {
                setSpeaking(true)
                speakChatMessage(context, settings, chunk, apiKey)
            } finally {
                setSpeaking(false)
                next.complete(Unit)
            }
        }
        chain = next
    }
}

private suspend fun transcribeVoiceFile(settings: AppSettings, apiKey: String?, file: File): String = withContext(Dispatchers.IO) {
    var lastError = "Trascrizione non disponibile"
    for (root in voiceGatewayRoots(settings)) {
        for (token in hermesAuthCandidates(apiKey)) {
            val body = MultipartBody.Builder()
                .setType(MultipartBody.FORM)
                .addFormDataPart("file", file.name, file.asRequestBody("audio/mp4".toMediaType()))
                .build()
            val request = Request.Builder()
                .url("$root/audio/transcriptions")
                .header("Accept", "application/json")
                .header("User-Agent", "HermesHub-Android")
                .apply { token?.let { header("Authorization", "Bearer $it") } }
                .post(body)
                .build()
            val response = try {
                voiceHttpClient.newCall(request).execute()
            } catch (ex: Exception) {
                lastError = ex.message ?: ex.javaClass.simpleName
                continue
            }
            response.use {
                val responseBody = it.body.string()
                if (it.isSuccessful) return@withContext JSONObject(responseBody).optString("text").trim()
                lastError = "HTTP ${it.code}: ${responseBody.take(180)}"
                if (it.code != 401) break
            }
        }
    }
    throw java.io.IOException(lastError)
}

private fun createVoiceRecorder(context: Context, file: File): MediaRecorder {
    @Suppress("DEPRECATION")
    val recorder = if (Build.VERSION.SDK_INT >= 31) MediaRecorder(context) else MediaRecorder()
    recorder.setAudioSource(MediaRecorder.AudioSource.MIC)
    recorder.setOutputFormat(MediaRecorder.OutputFormat.MPEG_4)
    recorder.setAudioEncoder(MediaRecorder.AudioEncoder.AAC)
    recorder.setAudioEncodingBitRate(128_000)
    recorder.setAudioSamplingRate(44_100)
    recorder.setOutputFile(file.absolutePath)
    return recorder
}

private fun MediaRecorder.runCatchingStopAndRelease() {
    runCatching { stop() }
    runCatching { reset() }
    runCatching { release() }
}

private fun voiceGatewayRoots(settings: AppSettings): List<String> {
    val roots = mutableListOf<String>()
    runCatching {
        val uri = URI(settings.gatewayUrl.trim())
        val scheme = uri.scheme ?: "http"
        val host = uri.host?.takeIf { it.isNotBlank() } ?: "100.94.223.14"
        val port = if (uri.port > 0) uri.port else 8642
        val path = uri.rawPath?.trimEnd('/')?.takeIf { it.isNotBlank() && it != "/" } ?: "/v1"
        roots += URI(scheme, null, host, port, path, null, null).toString().trimEnd('/')
    }
    roots += "http://100.94.223.14:8642/v1"
    roots += "http://hermes:8642/v1"
    roots += "http://hermes.local:8642/v1"
    return roots.distinctBy { it.lowercase() }
}

private fun findSpeechCut(text: String, flush: Boolean): Int {
    if (text.isBlank()) return -1
    val limit = minOf(text.length, 160)
    for (i in limit - 1 downTo 0) {
        if ((text[i] == '.' || text[i] == '!' || text[i] == '?' || text[i] == '\n') && i >= 14) return i + 1
    }
    if (text.length > 120) {
        val comma = text.lastIndexOfAny(charArrayOf(',', ';', ':', ' '), startIndex = minOf(text.length - 1, 120))
        return if (comma > 55) comma + 1 else 120
    }
    return if (flush) text.length else -1
}

private fun String.trimForStatus(): String {
    val clean = replace('\n', ' ').replace('\r', ' ').trim()
    return if (clean.length > 110) clean.take(110) + "..." else clean
}
