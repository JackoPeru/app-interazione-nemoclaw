package com.nemoclaw.chat

import androidx.compose.animation.core.LinearEasing
import androidx.compose.animation.core.RepeatMode
import androidx.compose.animation.core.animateFloat
import androidx.compose.animation.core.infiniteRepeatable
import androidx.compose.animation.core.rememberInfiniteTransition
import androidx.compose.animation.core.tween
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.rounded.AutoAwesome
import androidx.compose.material.icons.rounded.ExpandLess
import androidx.compose.material.icons.rounded.ExpandMore
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.Icon
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.TileMode
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp

@Composable
internal fun StreamingBubbleView(state: StreamingState) {
    Row(
        modifier = Modifier.fillMaxWidth(),
        horizontalArrangement = Arrangement.Start
    ) {
        Card(
            modifier = Modifier.fillMaxWidth(0.94f),
            colors = CardDefaults.cardColors(containerColor = AppColors.AssistantBubble),
            shape = RoundedCornerShape(20.dp)
        ) {
            Column(modifier = Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
                Text(
                    text = "Hermes",
                    color = Color.White,
                    fontWeight = FontWeight.SemiBold,
                    fontSize = 12.sp
                )

                if (state.hasThinking) {
                    ThinkingExpander(
                        thinking = state.thinking,
                        active = !state.thinkingFrozen,
                        elapsedSec = state.thinkingElapsedSec
                    )
                }

                state.toolCalls.forEach { tool ->
                    ToolCallCard(tool)
                }

                if (state.text.isNotEmpty()) {
                    Text(text = state.text, color = Color.White, fontSize = 14.sp)
                }

                state.visualBlocks.filter { it.isValidVisualBlock() }.forEach { block ->
                    VisualBlockView(block)
                }

                if (state.isDone) {
                    val parts = mutableListOf<String>()
                    state.stats?.ttftMs?.takeIf { it > 0 }?.let { parts += "TTFT ${"%.0f".format(it)}ms" }
                    state.stats?.tokensPerSecond?.takeIf { it > 0 }?.let { parts += "${"%.1f".format(it)} t/s" }
                    state.stats?.tokensOut?.takeIf { it > 0 }?.let { parts += "$it tok" }
                    state.stats?.promptTokens?.takeIf { it > 0 }?.let { parts += "prompt $it" }
                    state.stats?.totalMs?.takeIf { it > 0 }?.let { parts += "${"%.1f".format(it / 1000.0)}s" }
                    if (parts.isNotEmpty()) {
                        Text(
                            text = parts.joinToString("  ·  "),
                            color = AppColors.Muted,
                            fontSize = 11.sp
                        )
                    }
                }

                state.error?.let { err ->
                    if (state.text.isEmpty()) {
                        Text(text = err, color = AppColors.Muted, fontSize = 12.sp)
                    }
                }
            }
        }
    }
}

@Composable
internal fun ThinkingExpander(thinking: String, active: Boolean, elapsedSec: Double) {
    var expanded by remember { mutableStateOf(false) }
    Column(verticalArrangement = Arrangement.spacedBy(6.dp)) {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .clickable { expanded = !expanded },
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.spacedBy(6.dp)
        ) {
            if (active) {
                ShimmerText("Sto pensando")
            } else {
                val label = if (elapsedSec >= 1) "Pensato per ${"%.1f".format(elapsedSec)}s" else "Ragionamento"
                Text(text = label, color = AppColors.Muted, fontWeight = FontWeight.SemiBold, fontSize = 14.sp)
            }
            if (thinking.isNotEmpty()) {
                Icon(
                    imageVector = if (expanded) Icons.Rounded.ExpandLess else Icons.Rounded.ExpandMore,
                    contentDescription = "Mostra ragionamento",
                    tint = AppColors.Muted,
                    modifier = Modifier.size(16.dp)
                )
            }
        }
        if (expanded && thinking.isNotEmpty()) {
            Surface(
                color = AppColors.Surface,
                shape = RoundedCornerShape(12.dp)
            ) {
                Text(
                    modifier = Modifier
                        .heightIn(max = 220.dp)
                        .verticalScroll(rememberScrollState())
                        .padding(12.dp),
                    text = thinking,
                    color = AppColors.Muted,
                    fontSize = 12.sp
                )
            }
        }
    }
}

@Composable
internal fun ShimmerText(text: String) {
    val transition = rememberInfiniteTransition(label = "shimmer")
    val phase by transition.animateFloat(
        initialValue = 0f,
        targetValue = 1f,
        animationSpec = infiniteRepeatable(
            animation = tween(durationMillis = 1400, easing = LinearEasing),
            repeatMode = RepeatMode.Restart
        ),
        label = "phase"
    )
    val width = 600f
    val brush = Brush.linearGradient(
        colors = listOf(
            Color(0xFF6F7888),
            Color(0xFFFFFFFF),
            Color(0xFF6F7888)
        ),
        start = Offset(x = (phase - 0.5f) * width, y = 0f),
        end = Offset(x = (phase + 0.5f) * width, y = 0f),
        tileMode = TileMode.Clamp
    )
    Text(
        text = text,
        style = TextStyle(
            brush = brush,
            fontWeight = FontWeight.SemiBold,
            fontSize = 14.sp
        )
    )
}

@Composable
internal fun ToolCallCard(tool: ToolCallState) {
    Surface(
        modifier = Modifier.fillMaxWidth(),
        color = AppColors.Surface,
        shape = RoundedCornerShape(12.dp)
    ) {
        Column(modifier = Modifier.padding(12.dp), verticalArrangement = Arrangement.spacedBy(4.dp)) {
            Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                Icon(
                    imageVector = Icons.Rounded.AutoAwesome,
                    contentDescription = "Tool",
                    tint = AppColors.Accent,
                    modifier = Modifier.size(14.dp)
                )
                Text(
                    text = "Tool · ${tool.name}",
                    color = Color.White,
                    fontWeight = FontWeight.SemiBold,
                    fontSize = 12.sp
                )
                Text(text = tool.status, color = AppColors.Muted, fontSize = 11.sp)
            }
            if (tool.args.isNotEmpty()) {
                Text(
                    text = tool.args,
                    color = AppColors.Muted,
                    fontFamily = FontFamily.Monospace,
                    fontSize = 11.sp
                )
            }
            tool.result?.takeIf { it.isNotEmpty() }?.let { res ->
                Text(
                    text = res,
                    color = Color.White,
                    fontFamily = FontFamily.Monospace,
                    fontSize = 11.sp
                )
            }
        }
    }
}

internal data class SlashCommand(
    val display: String,
    val title: String,
    val description: String,
    val action: SlashAction
)

internal enum class SlashAction {
    ModeChat, ModeAgent, Clear, Help, Health,
    OpenServer, OpenOperator, OpenArchive, OpenTasks, OpenSettings, OpenAbout,
    PromptSetup, PromptVisual, PromptResearch, PromptWeb, PromptImage
}

internal fun slashCommands(): List<SlashCommand> = listOf(
    SlashCommand("/chat", "Modalita Chat", "Conversazione normale", SlashAction.ModeChat),
    SlashCommand("/agente", "Modalita Agente", "Esegui Runs/Jobs Hermes", SlashAction.ModeAgent),
    SlashCommand("/agent", "Modalita Agente", "Alias di /agente", SlashAction.ModeAgent),
    SlashCommand("/clear", "Pulisci chat", "Svuota conversazione corrente", SlashAction.Clear),
    SlashCommand("/new", "Nuova chat", "Inizia nuova conversazione", SlashAction.Clear),
    SlashCommand("/health", "Controlla Hermes", "Verifica health e capabilities", SlashAction.Health),
    SlashCommand("/server", "Apri Server", "Dashboard server", SlashAction.OpenServer),
    SlashCommand("/runs", "Apri Operator/Runs", "Probe API Hermes", SlashAction.OpenOperator),
    SlashCommand("/archive", "Apri Archivio", "Conversazioni salvate", SlashAction.OpenArchive),
    SlashCommand("/tasks", "Apri Task agente", "Coda jobs Hermes", SlashAction.OpenTasks),
    SlashCommand("/settings", "Impostazioni", "Pagina settings", SlashAction.OpenSettings),
    SlashCommand("/about", "Info app", "Versione, profilo", SlashAction.OpenAbout),
    SlashCommand("/setup", "Setup Hermes", "Prompt setup", SlashAction.PromptSetup),
    SlashCommand("/visual", "Spiegazione visiva", "Richiedi blocchi visuali", SlashAction.PromptVisual),
    SlashCommand("/research", "Deep research", "Approfondisci con fonti", SlashAction.PromptResearch),
    SlashCommand("/web", "Ricerca web", "Cerca sul web", SlashAction.PromptWeb),
    SlashCommand("/image", "Crea immagine", "Prepara richiesta immagine", SlashAction.PromptImage),
    SlashCommand("/help", "Aiuto", "Mostra comandi", SlashAction.Help)
)

internal fun filterSlashCommands(query: String): List<SlashCommand> {
    if (query.isEmpty() || !query.startsWith("/")) return emptyList()
    val q = query.lowercase()
    val needle = q.trimStart('/')
    return slashCommands().filter { cmd ->
        cmd.display.startsWith(q, ignoreCase = true) ||
            cmd.display.contains(needle, ignoreCase = true) ||
            cmd.title.contains(needle, ignoreCase = true)
    }.take(10)
}

@Composable
internal fun SlashCommandList(
    commands: List<SlashCommand>,
    onSelect: (SlashCommand) -> Unit
) {
    Surface(
        modifier = Modifier
            .fillMaxWidth()
            .padding(horizontal = 18.dp),
        color = AppColors.Elevated,
        shape = RoundedCornerShape(14.dp)
    ) {
        Column(modifier = Modifier.padding(6.dp), verticalArrangement = Arrangement.spacedBy(2.dp)) {
            commands.forEach { cmd ->
                Row(
                    modifier = Modifier
                        .fillMaxWidth()
                        .clickable { onSelect(cmd) }
                        .padding(horizontal = 10.dp, vertical = 8.dp),
                    verticalAlignment = Alignment.CenterVertically,
                    horizontalArrangement = Arrangement.spacedBy(10.dp)
                ) {
                    Column(modifier = Modifier.weight(1f)) {
                        Text(text = cmd.display, color = Color.White, fontWeight = FontWeight.SemiBold, fontSize = 13.sp)
                        Text(text = "${cmd.title} · ${cmd.description}", color = AppColors.Muted, fontSize = 11.sp)
                    }
                }
            }
        }
    }
}
