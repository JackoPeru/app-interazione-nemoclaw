package com.nemoclaw.chat

import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.Job
import kotlinx.coroutines.currentCoroutineContext
import kotlinx.coroutines.ensureActive
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.flow
import kotlinx.coroutines.flow.flowOn
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody
import okhttp3.Call
import org.json.JSONArray
import org.json.JSONObject
import java.util.concurrent.TimeUnit
import kotlin.math.max

data class ChatStreamStats(
    val ttftMs: Double? = null,
    val totalMs: Double? = null,
    val tokensOut: Int? = null,
    val tokensPerSecond: Double? = null,
    val promptTokens: Int? = null
)

data class ToolCallState(
    val id: String,
    val name: String,
    val args: String = "",
    val status: String = "in esecuzione…",
    val result: String? = null
)

data class StreamingState(
    val text: String = "",
    val thinking: String = "",
    val hasThinking: Boolean = false,
    val thinkingFrozen: Boolean = false,
    val thinkingElapsedSec: Double = 0.0,
    val toolCalls: List<ToolCallState> = emptyList(),
    val visualBlocks: List<VisualBlock> = emptyList(),
    val visualBlocksVersion: Int? = null,
    val responseId: String? = null,
    val stats: ChatStreamStats? = null,
    val status: String = "Invio prompt a Hermes...",
    val error: String? = null,
    val source: String = "Hermes",
    val usedFallback: Boolean = false,
    val isDone: Boolean = false,
    val startedAtNs: Long = System.nanoTime()
) {
    fun applyEvent(event: ChatStreamEvent): StreamingState = when (event) {
        is ChatStreamEvent.TextDelta -> {
            val merged = mergeTextDelta(text, event.delta)
            copy(
                text = merged,
                status = if (toolCalls.any { inferToolPendingStatus(it) }) status else "Hermes sta scrivendo la risposta...",
                thinkingFrozen = thinkingFrozen || hasThinking,
                thinkingElapsedSec = if (!thinkingFrozen && hasThinking) {
                    (System.nanoTime() - startedAtNs) / 1_000_000_000.0
                } else thinkingElapsedSec
            )
        }
        is ChatStreamEvent.ThinkingDelta -> copy(
            thinking = thinking + event.delta,
            hasThinking = true,
            thinkingFrozen = false,
            status = "Hermes sta ragionando..."
        )
        is ChatStreamEvent.ToolCallStart -> copy(
            status = "Tool in esecuzione: ${event.name}",
            toolCalls = if (toolCalls.any { it.id == event.id }) toolCalls
                else toolCalls + ToolCallState(event.id, event.name)
        )
        is ChatStreamEvent.ToolCallArgs -> copy(
            status = "Preparazione tool...",
            toolCalls = toolCalls.map {
                if (it.id == event.id) it.copy(args = it.args + event.delta) else it
            }
        )
        is ChatStreamEvent.ToolCallEnd -> copy(
            status = "Tool completato.",
            toolCalls = toolCalls.map {
                if (it.id == event.id) it.copy(status = "completato") else it
            }
        )
        is ChatStreamEvent.ToolResult -> copy(
            status = "Risultato tool ricevuto.",
            toolCalls = toolCalls.map {
                if (event.id != null && it.id == event.id) it.copy(result = event.output, status = "risultato pronto") else it
            }
        )
        is ChatStreamEvent.ResponseId -> copy(responseId = event.id)
        is ChatStreamEvent.VisualBlocks -> copy(
            visualBlocks = event.blocks,
            visualBlocksVersion = event.version
        )
        is ChatStreamEvent.Status -> copy(status = event.message)
        is ChatStreamEvent.Done -> copy(
            stats = event.stats,
            isDone = true,
            status = "Risposta completata.",
            thinkingFrozen = true,
            thinkingElapsedSec = if (thinkingElapsedSec > 0) thinkingElapsedSec
                else (System.nanoTime() - startedAtNs) / 1_000_000_000.0
        )
        is ChatStreamEvent.Error -> copy(error = event.message, status = "Errore Hermes.", isDone = true)
        is ChatStreamEvent.Usage -> this
    }
}

private fun mergeTextDelta(current: String, delta: String): String {
    if (delta.isEmpty()) return current
    if (current.isEmpty()) return delta
    if (delta == current) return current
    if (delta.startsWith(current)) return delta
    if (current.endsWith(delta)) return current
    return current + delta
}

private fun inferToolPendingStatus(tool: ToolCallState): Boolean {
    val s = tool.status.lowercase()
    return !s.contains("completat") && !s.contains("risultato") && !s.contains("success") && !s.contains("done") && !s.contains("fail") && !s.contains("error")
}

sealed class ChatStreamEvent {
    data class TextDelta(val delta: String) : ChatStreamEvent()
    data class ThinkingDelta(val delta: String) : ChatStreamEvent()
    data class ToolCallStart(val id: String, val name: String) : ChatStreamEvent()
    data class ToolCallArgs(val id: String, val delta: String) : ChatStreamEvent()
    data class ToolCallEnd(val id: String) : ChatStreamEvent()
    data class ToolResult(val id: String?, val name: String?, val output: String) : ChatStreamEvent()
    data class ResponseId(val id: String) : ChatStreamEvent()
    data class VisualBlocks(val blocks: List<VisualBlock>, val version: Int) : ChatStreamEvent()
    data class Status(val message: String) : ChatStreamEvent()
    data class Usage(val promptTokens: Int?, val completionTokens: Int?) : ChatStreamEvent()
    data class Done(val stats: ChatStreamStats) : ChatStreamEvent()
    data class Error(val message: String) : ChatStreamEvent()
}

fun streamChatRequest(
    settings: AppSettings,
    mode: String,
    prompt: String,
    history: List<ChatMessage>,
    conversationId: String?,
    previousResponseId: String?,
    apiKey: String?
): Flow<ChatStreamEvent> = flow {
    val start = System.nanoTime()
    var sawDelta = false
    var ttftMs: Double? = null
    var promptTokens: Int? = null
    var completionTokens: Int? = null
    val accumText = StringBuilder()
    val accumThink = StringBuilder()
    var lastError: String? = null

    suspend fun emitAndTrack(ev: ChatStreamEvent): Boolean {
        when (ev) {
            is ChatStreamEvent.TextDelta -> {
                if (!sawDelta) {
                    ttftMs = (System.nanoTime() - start) / 1_000_000.0
                    sawDelta = true
                }
                accumText.append(ev.delta)
            }
            is ChatStreamEvent.ThinkingDelta -> {
                if (!sawDelta) {
                    ttftMs = (System.nanoTime() - start) / 1_000_000.0
                    sawDelta = true
                }
                accumThink.append(ev.delta)
            }
            is ChatStreamEvent.Usage -> {
                promptTokens = ev.promptTokens ?: promptTokens
                completionTokens = ev.completionTokens ?: completionTokens
                return false
            }
            is ChatStreamEvent.Error -> {
                lastError = ev.message
                return true
            }
            else -> Unit
        }
        emit(ev)
        return false
    }

    emit(ChatStreamEvent.Status("Invio diretto a Hermes Responses API..."))
    val responsePayload = JSONObject()
        .put("model", settings.model)
        .put("input", prompt)
        .put(
            "instructions",
            if (mode.equals("Agente", ignoreCase = true))
                hermesHubAgentInstructions()
            else
                hermesHubChatInstructions()
        )
        .put("store", true)
        .put("stream", true)
        .put("conversation", conversationId ?: JSONObject.NULL)
        .put("previous_response_id", previousResponseId ?: JSONObject.NULL)
        .put("metadata", visualBlocksMetadataJson(settings))

    val responseUrl = "${settings.gatewayUrl.trimEnd('/')}/responses"
    val terminated = openSseStream(responseUrl, responsePayload, apiKey, "Hermes Responses API") { ev ->
        emitAndTrack(ev)
    }
    if (terminated && lastError != null && !sawDelta) {
        emit(ChatStreamEvent.Status("Responses API non disponibile, fallback rapido a Chat Completions..."))
    }

    if (!sawDelta) {
        emit(ChatStreamEvent.Status("Fallback a Hermes Chat Completions..."))
        val payload = JSONObject()
            .put("model", settings.model)
            .put("stream", true)
            .put("metadata", visualBlocksMetadataJson(settings))
            .put("messages", JSONArray().apply {
                put(
                    JSONObject()
                        .put("role", "system")
                        .put("content", if (mode.equals("Agente", ignoreCase = true)) hermesHubAgentInstructions() else hermesHubChatInstructions())
                )
                history.filter { !it.isAction }.forEach { msg ->
                    put(
                        JSONObject()
                            .put("role", if (msg.fromUser) "user" else "assistant")
                            .put("content", msg.text)
                    )
                }
            })
        val url = "${settings.gatewayUrl.trimEnd('/')}/chat/completions"
        openSseStream(url, payload, apiKey, "Hermes Chat Completions") { ev ->
            emitAndTrack(ev)
        }
    }

    val totalMs = (System.nanoTime() - start) / 1_000_000.0
    if (!sawDelta) {
        emit(ChatStreamEvent.Error(lastError ?: "Stream Hermes vuoto."))
        return@flow
    }

    val tokensOut = completionTokens ?: max(1, accumText.length / 4)
    val sinceFirst = if (ttftMs != null) max(1.0, totalMs - ttftMs!!) else totalMs
    val tps = if (tokensOut > 0 && sinceFirst > 0) tokensOut / (sinceFirst / 1000.0) else null
    emit(ChatStreamEvent.Done(ChatStreamStats(ttftMs, totalMs, tokensOut, tps, promptTokens)))
}.flowOn(Dispatchers.IO)

private suspend fun streamSupportsResponses(settings: AppSettings, apiKey: String?): Boolean {
    return try {
        val client = OkHttpClient.Builder()
            .connectTimeout(8, TimeUnit.SECONDS)
            .readTimeout(8, TimeUnit.SECONDS)
            .build()
        val builder = Request.Builder()
            .url("${settings.gatewayUrl.trimEnd('/')}/capabilities")
            .header("Accept", "application/json")
            .header("User-Agent", "HermesHub-Android")
        if (!apiKey.isNullOrBlank()) {
            builder.header("Authorization", "Bearer ${apiKey.trim()}")
        }
        client.newCall(builder.get().build()).execute().use { response ->
            val body = response.body?.string().orEmpty()
            if (!response.isSuccessful || body.isBlank()) {
                true
            } else {
                body.contains("responses", ignoreCase = true)
            }
        }
    } catch (_: Exception) {
        true
    }
}

private suspend inline fun openSseStream(
    url: String,
    payload: JSONObject,
    apiKey: String?,
    label: String,
    crossinline onEvent: suspend (ChatStreamEvent) -> Boolean
): Boolean {
    var call: Call? = null
    val cancellationHook = currentCoroutineContext()[Job]?.invokeOnCompletion { cause ->
        if (cause is CancellationException) {
            call?.cancel()
        }
    }
    return try {
        val client = OkHttpClient.Builder()
            .connectTimeout(15, TimeUnit.SECONDS)
            .readTimeout(60, TimeUnit.MINUTES)
            .writeTimeout(60, TimeUnit.SECONDS)
            .callTimeout(60, TimeUnit.MINUTES)
            .build()
        val builder = Request.Builder()
            .url(url)
            .header("Accept", "text/event-stream, application/json")
            .header("User-Agent", "HermesHub-Android")
        if (!apiKey.isNullOrBlank()) {
            builder.header("Authorization", "Bearer ${apiKey.trim()}")
        }
        val body = payload.toString().toRequestBody("application/json; charset=utf-8".toMediaType())
        val request = builder.post(body).build()
        call = client.newCall(request)
        call!!.execute().use { response ->
            if (!response.isSuccessful) {
                val text = response.body?.string().orEmpty().take(240)
                onEvent(ChatStreamEvent.Error("$label HTTP ${response.code}: $text"))
                return@use true
            }
            onEvent(ChatStreamEvent.Status("$label connesso. Attendo eventi..."))
            val ct = response.body?.contentType()?.toString().orEmpty()
            if (!ct.contains("event-stream", ignoreCase = true)) {
                val full = response.body?.string().orEmpty()
                parseFullBody(full).forEach { onEvent(it) }
                return@use false
            }
            val source = response.body?.source() ?: return@use false
            val dataBuf = StringBuilder()
            var eventName: String? = null
            while (true) {
                currentCoroutineContext().ensureActive()
                val line = source.readUtf8Line() ?: break
                if (line.isEmpty()) {
                    if (dataBuf.isNotEmpty()) {
                        parseSseData(eventName, dataBuf.toString()).forEach { onEvent(it) }
                        dataBuf.clear()
                        eventName = null
                    }
                    continue
                }
                when {
                    line.startsWith(":") -> Unit
                    line.startsWith("event:", ignoreCase = true) -> {
                        eventName = line.substring(6).trim()
                    }
                    line.startsWith("data:", ignoreCase = true) -> {
                        if (dataBuf.isNotEmpty()) dataBuf.append('\n')
                        dataBuf.append(line.substring(5).trimStart())
                    }
                }
            }
            if (dataBuf.isNotEmpty()) {
                parseSseData(eventName, dataBuf.toString()).forEach { onEvent(it) }
            }
            false
        }
    } catch (ex: CancellationException) {
        throw ex
    } catch (ex: Exception) {
        onEvent(ChatStreamEvent.Error("$label: ${ex.message ?: ex.javaClass.simpleName}"))
        true
    } finally {
        cancellationHook?.dispose()
    }
}

private fun parseSseData(eventName: String?, data: String): List<ChatStreamEvent> {
    if (data.isBlank() || data.trim() == "[DONE]") return emptyList()
    val json = try { JSONObject(data) } catch (_: Exception) {
        return listOf(ChatStreamEvent.TextDelta(data))
    }
    return parseEventObject(eventName, json)
}

private fun parseEventObject(eventName: String?, obj: JSONObject): List<ChatStreamEvent> {
    val out = mutableListOf<ChatStreamEvent>()
    val type = eventName ?: obj.optString("type", "")
    val t = type.lowercase()

    when {
        t.contains("output_text") && t.contains("delta") -> {
            val delta = obj.optString("delta", obj.optString("text", ""))
            if (delta.isNotEmpty()) out += ChatStreamEvent.TextDelta(delta)
            return out
        }
        t.contains("reasoning") && t.contains("delta") -> {
            val delta = obj.optString("delta", obj.optString("text", ""))
            if (delta.isNotEmpty()) out += ChatStreamEvent.ThinkingDelta(delta)
            return out
        }
        t.contains("function_call") && t.contains("arguments") && t.contains("delta") -> {
            val id = obj.optString("item_id", obj.optString("id", "tool"))
            val delta = obj.optString("delta", "")
            if (delta.isNotEmpty()) out += ChatStreamEvent.ToolCallArgs(id, delta)
            return out
        }
        t.contains("output_item.added") -> {
            val item = obj.optJSONObject("item")
            if (item != null) {
                val itype = item.optString("type", "")
                if (itype.contains("function", ignoreCase = true) || itype.contains("tool", ignoreCase = true)) {
                    val id = item.optString("id", "tool")
                    val name = item.optString("name", "tool")
                    out += ChatStreamEvent.ToolCallStart(id, name)
                }
            }
            return out
        }
        t.contains("output_item.done") || t.contains("function_call.completed") -> {
            val item = obj.optJSONObject("item")
            if (item != null) {
                val itemType = item.optString("type", "")
                if (itemType.contains("function", ignoreCase = true) || itemType.contains("tool", ignoreCase = true)) {
                    val id = item.optString("id", "tool")
                    out += ChatStreamEvent.ToolCallEnd(id)
                } else {
                    val text = extractTextFromAnyJson(item)
                    if (text.isNotBlank()) out += ChatStreamEvent.TextDelta(text)
                }
            }
            return out
        }
        t.contains("response.created") || t.contains("response.started") -> {
            val resp = obj.optJSONObject("response")
            if (resp != null) {
                val rid = resp.optString("id")
                if (rid.isNotBlank()) out += ChatStreamEvent.ResponseId(rid)
            }
            return out
        }
        t.contains("response.completed") || t.contains("response.done") -> {
            val resp = obj.optJSONObject("response")
            if (resp != null) {
                val rid = resp.optString("id")
                if (rid.isNotBlank()) out += ChatStreamEvent.ResponseId(rid)
                val usage = resp.optJSONObject("usage")
                if (usage != null) {
                    out += ChatStreamEvent.Usage(
                        usage.optIntOrNull("input_tokens") ?: usage.optIntOrNull("prompt_tokens"),
                        usage.optIntOrNull("output_tokens") ?: usage.optIntOrNull("completion_tokens")
                    )
                }
                val text = extractTextFromAnyJson(resp)
                if (text.isNotBlank()) out += ChatStreamEvent.TextDelta(text)
                val blocks = extractVisualBlocksFromJson(resp.toString())
                if (blocks.isNotEmpty()) {
                    out += ChatStreamEvent.VisualBlocks(blocks, VISUAL_BLOCKS_VERSION)
                }
            }
            return out
        }
    }

    // Chat Completions delta
    val choices = obj.optJSONArray("choices")
    if (choices != null) {
        for (i in 0 until choices.length()) {
            val choice = choices.optJSONObject(i) ?: continue
            val delta = choice.optJSONObject("delta")
            if (delta != null) {
                val content = delta.optString("content", "")
                if (content.isNotEmpty()) out += ChatStreamEvent.TextDelta(content)
                val reasoning = if (delta.has("reasoning")) delta.optString("reasoning", "") else delta.optString("reasoning_content", "")
                if (reasoning.isNotEmpty()) out += ChatStreamEvent.ThinkingDelta(reasoning)
                val toolCalls = delta.optJSONArray("tool_calls")
                if (toolCalls != null) {
                    for (j in 0 until toolCalls.length()) {
                        val call = toolCalls.optJSONObject(j) ?: continue
                        val id = call.optString("id", "tool")
                        val fn = call.optJSONObject("function")
                        if (fn != null) {
                            val name = fn.optString("name", "")
                            if (name.isNotBlank()) out += ChatStreamEvent.ToolCallStart(id, name)
                            val args = fn.optString("arguments", "")
                            if (args.isNotEmpty()) out += ChatStreamEvent.ToolCallArgs(id, args)
                        }
                    }
                }
            }
        }
        val usage = obj.optJSONObject("usage")
        if (usage != null) {
            out += ChatStreamEvent.Usage(
                usage.optIntOrNull("input_tokens") ?: usage.optIntOrNull("prompt_tokens"),
                usage.optIntOrNull("output_tokens") ?: usage.optIntOrNull("completion_tokens")
            )
        }
        val blocks = extractVisualBlocksFromJson(obj.toString())
        if (blocks.isNotEmpty()) {
            out += ChatStreamEvent.VisualBlocks(blocks, VISUAL_BLOCKS_VERSION)
        }
    } else {
        val text = extractTextFromAnyJson(obj)
        if (text.isNotBlank()) {
            out += ChatStreamEvent.TextDelta(text)
        }
    }

    return out
}

private fun extractTextFromAnyJson(obj: JSONObject): String {
    val direct = listOf("output_text", "text", "message", "reply", "result", "summary")
        .firstNotNullOfOrNull { key ->
            val value = obj.opt(key)
            if (value is String && value.isNotBlank()) value else null
        }
    if (!direct.isNullOrBlank()) return direct

    val content = obj.optJSONArray("content")
    if (content != null) {
        val builder = StringBuilder()
        for (i in 0 until content.length()) {
            val part = content.optJSONObject(i) ?: continue
            val partText = part.optString("text", part.optString("output_text", ""))
            if (partText.isNotBlank()) builder.append(partText)
        }
        if (builder.isNotEmpty()) return builder.toString()
    }

    val output = obj.optJSONArray("output")
    if (output != null) {
        val builder = StringBuilder()
        for (i in 0 until output.length()) {
            val item = output.optJSONObject(i) ?: continue
            val nested = extractTextFromAnyJson(item)
            if (nested.isNotBlank()) builder.append(nested)
        }
        if (builder.isNotEmpty()) return builder.toString()
    }

    val response = obj.optJSONObject("response")
    if (response != null) {
        val nested = extractTextFromAnyJson(response)
        if (nested.isNotBlank()) return nested
    }

    val item = obj.optJSONObject("item")
    if (item != null) {
        val nested = extractTextFromAnyJson(item)
        if (nested.isNotBlank()) return nested
    }

    return ""
}

private fun parseFullBody(body: String): List<ChatStreamEvent> {
    if (body.isBlank()) return emptyList()
    val out = mutableListOf<ChatStreamEvent>()
    val text = extractAssistantText(body)
    val rid = extractResponseId(body)
    if (rid != null) out += ChatStreamEvent.ResponseId(rid)
    if (text.isNotEmpty()) out += ChatStreamEvent.TextDelta(text)
    val blocks = try { extractVisualBlocks(body) } catch (_: Exception) { emptyList() }
    if (blocks.isNotEmpty()) out += ChatStreamEvent.VisualBlocks(blocks, VISUAL_BLOCKS_VERSION)
    return out
}

private fun extractVisualBlocksFromJson(json: String): List<VisualBlock> {
    return try { extractVisualBlocks(json) } catch (_: Exception) { emptyList() }
}

private fun JSONObject.optIntOrNull(key: String): Int? {
    if (!has(key) || isNull(key)) return null
    return try { getInt(key) } catch (_: Exception) { null }
}

private fun visualBlocksMetadataJson(settings: AppSettings): JSONObject {
    return JSONObject()
        .put("client", "hermes-hub")
        .put("client_surface", "android-app")
        .put("profile", "Matteo")
        .put(
            "memory_policy",
            JSONObject()
                .put("scope", "shared-hermes-agent-memory")
                .put("share_with_cli", true)
                .put("use_server_memory_tools", true)
                .put("do_not_create_app_only_memory", true)
        )
        .put(
            "hub_sections",
            JSONObject()
                .put("chat", "Conversazione principale Hermes Hub.")
                .put("video", "Feed personale video: output resta sul PC/Hermes, app usa stream_url/download_url e feedback.")
                .put("news", "Feed personale articoli: Hermes produce articoli con fonti, app salva feedback.")
                .put("jobs", "Coda Hermes Jobs condivisa con CLI/server.")
                .put("runs", "Runs operative Hermes.")
        )
        .put(
            "visual_blocks",
            JSONObject()
                .put("min_supported_version", VISUAL_BLOCKS_VERSION)
                .put("max_supported_version", VISUAL_BLOCKS_VERSION)
                .put("mode", settings.visualBlocksMode)
                .put("image_gallery", "supported via /v1/media proxy URLs only")
        )
}
