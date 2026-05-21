package com.nemoclaw.chat

internal const val HERMES_FALLBACK_API_KEY = "hermes-hub"

internal fun hermesAuthRetryCandidates(apiKey: String?): List<String> {
    val candidates = linkedSetOf<String>()
    apiKey?.trim()?.takeIf { it.isNotEmpty() }?.let(candidates::add)
    candidates += HERMES_FALLBACK_API_KEY
    return candidates.toList()
}

internal fun hermesAuthCandidates(apiKey: String?, allowCompatAuth: Boolean = true): List<String?> {
    return if (allowCompatAuth) hermesAuthRetryCandidates(apiKey) + listOf(null)
    else listOfNotNull(apiKey?.trim()?.takeIf { it.isNotEmpty() })
}

internal fun shouldUseResponsesFirst(settings: AppSettings, mode: String): Boolean {
    if (settings.preferredApi.equals("hermes-native", ignoreCase = true)) return true
    return settings.preferredApi.equals("openai-responses", ignoreCase = true) &&
        mode.equals("Agente", ignoreCase = true)
}

internal fun isHermesNative(settings: AppSettings): Boolean {
    return settings.preferredApi.equals("hermes-native", ignoreCase = true)
}

internal fun shouldRetryHermesWithBearerAuth(code: Int, body: String): Boolean {
    if (code != 401) return false
    val normalized = body.lowercase()
    return normalized.contains("invalid api key") ||
        normalized.contains("invalid_api_key") ||
        normalized.contains("invalidapikey")
}
