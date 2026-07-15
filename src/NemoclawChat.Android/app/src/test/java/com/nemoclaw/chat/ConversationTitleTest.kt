package com.nemoclaw.chat

import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class ConversationTitleTest {
    @Test
    fun normalizesAgentTitleWithoutPrefixOrQuotes() {
        assertEquals(
            "Configurazione Hermes su Tailscale",
            normalizeGeneratedConversationTitle("Titolo: \"Configurazione Hermes su Tailscale.\"", "Fallback")
        )
    }

    @Test
    fun usesFirstNonEmptyLineAndLimitsLength() {
        val title = normalizeGeneratedConversationTitle("\n\nTitolo molto lungo ".repeat(12), "Fallback")
        assertTrue(title.length <= 71)
        assertTrue(title.startsWith("Titolo molto lungo"))
    }

    @Test
    fun fallsBackWhenAgentReturnsNothing() {
        assertEquals("Fallback semantico", normalizeGeneratedConversationTitle("   ", "Fallback semantico"))
    }
}
