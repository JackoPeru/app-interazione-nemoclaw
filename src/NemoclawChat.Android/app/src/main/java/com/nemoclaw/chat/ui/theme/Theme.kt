package com.nemoclaw.chat.ui.theme

import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.darkColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.graphics.Color

private val DarkScheme = darkColorScheme(
    background = Color(0xFF0F1115),
    surface = Color(0xFF1A1E26),
    primary = Color(0xFFF5A524),
    onBackground = Color.White,
    onSurface = Color.White,
    onPrimary = Color(0xFF0A0F16)
)

@Composable
fun ChatClawTheme(content: @Composable () -> Unit) {
    MaterialTheme(
        colorScheme = DarkScheme,
        content = content
    )
}
