package com.nemoclaw.chat

import android.app.PendingIntent
import android.appwidget.AppWidgetManager
import android.appwidget.AppWidgetProvider
import android.content.Context
import android.content.Intent
import android.widget.RemoteViews
import androidx.core.net.toUri

class HermesWidgetProvider : AppWidgetProvider() {
    override fun onUpdate(context: Context, manager: AppWidgetManager, ids: IntArray) {
        ids.forEach { id ->
            val views = RemoteViews(context.packageName, R.layout.hermes_widget)
            views.setOnClickPendingIntent(R.id.widget_chat, deepLink(context, "hermes-hub://chat", 1))
            views.setOnClickPendingIntent(R.id.widget_voice, deepLink(context, "hermes-hub://voice", 2))
            views.setOnClickPendingIntent(R.id.widget_camera, deepLink(context, "hermes-hub://chat?prompt=Scansiona%20e%20analizza%20un%20documento.", 3))
            manager.updateAppWidget(id, views)
        }
    }
    private fun deepLink(context: Context, url: String, request: Int): PendingIntent = PendingIntent.getActivity(context, request, Intent(Intent.ACTION_VIEW, url.toUri(), context, MainActivity::class.java), PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE)
}
