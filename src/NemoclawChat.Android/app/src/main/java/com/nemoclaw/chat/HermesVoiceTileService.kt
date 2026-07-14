package com.nemoclaw.chat

import android.app.PendingIntent
import android.content.Intent
import android.os.Build
import android.service.quicksettings.TileService
import androidx.core.net.toUri

class HermesVoiceTileService : TileService() {
    @Suppress("StartActivityAndCollapseDeprecated", "DEPRECATION")
    override fun onClick() {
        super.onClick()
        val intent = Intent(Intent.ACTION_VIEW, "hermes-hub://voice".toUri(), this, MainActivity::class.java).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
        if (Build.VERSION.SDK_INT >= 34) {
            val pendingIntent = PendingIntent.getActivity(this, 8642, intent, PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE)
            startActivityAndCollapse(pendingIntent)
        } else {
            startActivityAndCollapse(intent)
        }
    }
}
