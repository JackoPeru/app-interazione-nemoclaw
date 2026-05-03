package com.nemoclaw.chat.ui.theme

import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.darkColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.graphics.Color

private val DarkScheme = darkColorScheme(
    background = Color(0xFF212121),
    surface = Color(0xFF2B2B2B),
    primary = Color(0xFF3EA6FF),
    onBackground = Color.White,
    onSurface = Color.White,
    onPrimary = Color.White
)

@Composable
fun NemoclawTheme(content: @Composable () -> Unit) {
    MaterialTheme(
        colorScheme = DarkScheme,
        content = content
    )
}
