package com.nemoclaw.chat

import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import androidx.core.app.RemoteInput

class HermesNotificationReplyReceiver : BroadcastReceiver() {
    override fun onReceive(context: Context, intent: Intent) {
        val reply = RemoteInput.getResultsFromIntent(intent)?.getCharSequence("hermes_reply")?.toString()?.trim().orEmpty()
        if (reply.isBlank()) return
        context.startActivity(Intent(context, MainActivity::class.java).putExtra("notification_reply", reply).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK or Intent.FLAG_ACTIVITY_CLEAR_TOP))
    }
}
