package com.nemoclaw.chat

import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateListOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.compose.runtime.snapshots.SnapshotStateList

internal class ChatStateHolder {
    val messages: SnapshotStateList<ChatMessage> = mutableStateListOf()
    var draft: String by mutableStateOf("")
    var mode: String by mutableStateOf("Chat")
    var activeConversationId: String? by mutableStateOf(null)
    var previousResponseId: String? by mutableStateOf(null)
    var streamingState: StreamingState? by mutableStateOf(null)
    var sending: Boolean by mutableStateOf(false)

    fun resetForNewChat() {
        messages.clear()
        activeConversationId = null
        previousResponseId = null
        streamingState = null
        sending = false
        draft = ""
    }
}
