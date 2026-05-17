package com.nemoclaw.chat

internal const val HERMES_FALLBACK_API_KEY = "hermes-hub"

internal fun hermesAuthRetryCandidates(apiKey: String?): List<String> {
    val candidates = linkedSetOf<String>()
    apiKey?.trim()?.takeIf { it.isNotEmpty() }?.let(candidates::add)
    candidates += HERMES_FALLBACK_API_KEY
    return candidates.toList()
}

internal fun hermesAuthCandidates(apiKey: String?): List<String?> {
    return hermesAuthRetryCandidates(apiKey) + listOf(null)
}

internal fun shouldUseResponsesFirst(settings: AppSettings, mode: String): Boolean {
    return settings.preferredApi.equals("openai-responses", ignoreCase = true) &&
        mode.equals("Agente", ignoreCase = true)
}

internal fun shouldRetryHermesWithBearerAuth(code: Int, body: String): Boolean {
    if (code != 401) return false
    val normalized = body.lowercase()
    return normalized.contains("invalid api key") ||
        normalized.contains("invalid_api_key") ||
        normalized.contains("invalidapikey")
}
