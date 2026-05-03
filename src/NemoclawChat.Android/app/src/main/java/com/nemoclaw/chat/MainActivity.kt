package com.nemoclaw.chat

import android.content.Context
import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.imePadding
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.DropdownMenu
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.IconButton
import androidx.compose.material3.NavigationBar
import androidx.compose.material3.NavigationBarItem
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Surface
import androidx.compose.material3.Switch
import androidx.compose.material3.Text
import androidx.compose.material3.TextField
import androidx.compose.material3.TextFieldDefaults
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateListOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.nemoclaw.chat.ui.theme.NemoclawTheme

class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContent {
            NemoclawTheme {
                ChatApp()
            }
        }
    }
}

private enum class Tab(val label: String) {
    Chat("Chat"),
    Tasks("Ordini"),
    Server("Server"),
    Settings("Impostazioni")
}

private data class ChatMessage(
    val author: String,
    val text: String,
    val fromUser: Boolean
)

private data class AppSettings(
    val gatewayUrl: String = AppDefaults.gatewayUrl,
    val provider: String = AppDefaults.provider,
    val inferenceEndpoint: String = AppDefaults.inferenceEndpoint,
    val preferredApi: String = AppDefaults.preferredApi,
    val model: String = AppDefaults.model,
    val accessMode: String = AppDefaults.accessMode,
    val demoMode: Boolean = true
)

@Composable
private fun ChatApp() {
    val context = LocalContext.current
    var selectedTab by remember { mutableStateOf(Tab.Chat) }
    var settings by remember { mutableStateOf(loadSettings(context)) }

    Scaffold(
        containerColor = AppColors.Background,
        bottomBar = {
            NavigationBar(containerColor = AppColors.Sidebar) {
                Tab.entries.forEach { tab ->
                    NavigationBarItem(
                        selected = selectedTab == tab,
                        onClick = { selectedTab = tab },
                        icon = { Text(tab.label.first().toString()) },
                        label = { Text(tab.label, maxLines = 1, overflow = TextOverflow.Ellipsis) }
                    )
                }
            }
        }
    ) { padding ->
        Box(
            modifier = Modifier
                .fillMaxSize()
                .padding(padding)
                .background(AppColors.Background)
        ) {
            when (selectedTab) {
                Tab.Chat -> ChatScreen(settings)
                Tab.Tasks -> PlaceholderScreen("Ordini agente", "Task, tool call, comandi e richieste approve/deny.", settings)
                Tab.Server -> PlaceholderScreen("Server", "Gateway, modello locale, sandbox e policy rete.", settings)
                Tab.Settings -> SettingsScreen(
                    settings = settings,
                    onSave = {
                        settings = it
                        saveSettings(context, it)
                    },
                    onReset = {
                        settings = AppSettings()
                        saveSettings(context, settings)
                    }
                )
            }
        }
    }
}

@Composable
private fun ChatScreen(settings: AppSettings) {
    val messages = remember { mutableStateListOf<ChatMessage>() }
    var draft by remember { mutableStateOf("") }
    var mode by remember { mutableStateOf("Chat") }

    Column(
        modifier = Modifier
            .fillMaxSize()
            .imePadding()
            .navigationBarsPadding()
    ) {
        TopBar(settings)
        Box(modifier = Modifier.weight(1f)) {
            if (messages.isEmpty()) {
                EmptyState(onPrompt = { draft = it })
            }

            LazyColumn(
                modifier = Modifier.fillMaxSize(),
                contentPadding = androidx.compose.foundation.layout.PaddingValues(18.dp, 24.dp),
                verticalArrangement = Arrangement.spacedBy(14.dp)
            ) {
                items(messages) { message ->
                    MessageBubble(message)
                }
            }
        }
        Composer(
            value = draft,
            mode = mode,
            onValueChange = { draft = it },
            onAction = { draft = appendPrompt(draft, it) },
            onModeChange = { mode = it },
            onSend = {
                val text = draft.trim()
                if (text.isNotEmpty()) {
                    messages.add(ChatMessage("Tu", text, true))
                    messages.add(ChatMessage("NemoClaw", demoReply(settings, mode), false))
                    draft = ""
                }
            }
        )
    }
}

@Composable
private fun TopBar(settings: AppSettings) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .height(58.dp)
            .padding(horizontal = 18.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        Text(
            text = "Nemoclaw",
            color = Color.White,
            fontWeight = FontWeight.SemiBold,
            fontSize = 18.sp
        )
        Spacer(modifier = Modifier.weight(1f))
        Surface(color = AppColors.Surface, shape = RoundedCornerShape(18.dp)) {
            Text(
            modifier = Modifier.padding(horizontal = 12.dp, vertical = 6.dp),
                text = if (settings.demoMode) "Demo: ${settings.gatewayUrl}" else settings.gatewayUrl,
                color = AppColors.Muted,
                fontSize = 12.sp
            )
        }
    }
}

@Composable
private fun EmptyState(onPrompt: (String) -> Unit) {
    Column(
        modifier = Modifier
            .fillMaxSize()
            .padding(horizontal = 24.dp),
        verticalArrangement = Arrangement.Center,
        horizontalAlignment = Alignment.CenterHorizontally
    ) {
        Text(
            text = "Che vuoi fare oggi, Matteo?",
            color = Color.White,
            fontWeight = FontWeight.SemiBold,
            fontSize = 24.sp
        )
        Spacer(modifier = Modifier.height(10.dp))
        Text(
            text = "Chat normale o ordine agente verso il tuo home-server NemoClaw.",
            color = AppColors.Muted,
            fontSize = 14.sp
        )
        Spacer(modifier = Modifier.height(18.dp))
        Column(verticalArrangement = Arrangement.spacedBy(10.dp)) {
            SuggestionButton("Prepara setup NemoClaw") {
                onPrompt("Preparami i passaggi per avviare NemoClaw con un endpoint OpenAI-compatible locale.")
            }
            SuggestionButton("Controlla server") {
                onPrompt("Controlla stato gateway, modello locale e sandbox NemoClaw.")
            }
            SuggestionButton("Crea ordine agente") {
                onPrompt("Crea un task agente sicuro con richiesta approve/deny prima di ogni azione rischiosa.")
            }
        }
    }
}

@Composable
private fun SuggestionButton(text: String, onClick: () -> Unit) {
    Surface(
        modifier = Modifier.clickable(onClick = onClick),
        color = AppColors.Surface,
        shape = RoundedCornerShape(18.dp)
    ) {
        Text(
            modifier = Modifier.padding(horizontal = 14.dp, vertical = 9.dp),
            text = text,
            color = Color.White,
            fontSize = 14.sp
        )
    }
}

@Composable
private fun MessageBubble(message: ChatMessage) {
    Row(
        modifier = Modifier.fillMaxWidth(),
        horizontalArrangement = if (message.fromUser) Arrangement.End else Arrangement.Start
    ) {
        Card(
            modifier = Modifier.fillMaxWidth(if (message.fromUser) 0.86f else 0.92f),
            colors = CardDefaults.cardColors(
                containerColor = if (message.fromUser) AppColors.UserBubble else AppColors.AssistantBubble
            ),
            shape = RoundedCornerShape(20.dp)
        ) {
            Column(modifier = Modifier.padding(16.dp)) {
                Text(
                    text = message.author,
                    color = Color.White,
                    fontWeight = FontWeight.SemiBold,
                    fontSize = 12.sp
                )
                Spacer(modifier = Modifier.height(6.dp))
                Text(text = message.text, color = Color.White)
            }
        }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun Composer(
    value: String,
    mode: String,
    onValueChange: (String) -> Unit,
    onAction: (String) -> Unit,
    onModeChange: (String) -> Unit,
    onSend: () -> Unit
) {
    var expanded by remember { mutableStateOf(false) }

    Surface(
        modifier = Modifier
            .fillMaxWidth()
            .padding(horizontal = 12.dp, vertical = 10.dp)
            .widthIn(max = 1040.dp),
        color = AppColors.Composer,
        shape = RoundedCornerShape(30.dp)
    ) {
        Column(modifier = Modifier.padding(horizontal = 10.dp, vertical = 7.dp)) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                verticalAlignment = Alignment.CenterVertically
            ) {
                Box {
                    IconButton(onClick = { expanded = true }) {
                        Text("+", color = AppColors.Muted, fontSize = 26.sp)
                    }
                    DropdownMenu(
                        expanded = expanded,
                        onDismissRequest = { expanded = false },
                        containerColor = AppColors.Surface
                    ) {
                        DropdownMenuItem(text = { Text("Aggiungi file al task", color = Color.White) }, onClick = {
                            expanded = false
                            onAction("Allega un file al prossimo task e analizzalo nel contesto NemoClaw.")
                        })
                        DropdownMenuItem(text = { Text("Cattura screenshot", color = Color.White) }, onClick = {
                            expanded = false
                            onAction("Usa uno screenshot come contesto visivo per capire app o server.")
                        })
                        DropdownMenuItem(text = { Text("Scatta foto", color = Color.White) }, onClick = {
                            expanded = false
                            onAction("Acquisisci una foto e usala come allegato per la conversazione.")
                        })
                        DropdownMenuItem(text = { Text("Modalita: Chat", color = Color.White) }, onClick = {
                            expanded = false
                            onModeChange("Chat")
                        })
                        DropdownMenuItem(text = { Text("Modalita: Agente", color = Color.White) }, onClick = {
                            expanded = false
                            onModeChange("Agente")
                        })
                        DropdownMenuItem(text = { Text("Deep Research locale", color = Color.White) }, onClick = {
                            expanded = false
                            onAction("Esegui una ricerca approfondita e cita fonti, usando rete solo dopo approvazione.")
                        })
                        DropdownMenuItem(text = { Text("Ricerca web autorizzata", color = Color.White) }, onClick = {
                            expanded = false
                            onAction("Cerca sul web informazioni aggiornate, chiedendo conferma prima di uscire dalla LAN/VPN.")
                        })
                        DropdownMenuItem(text = { Text("Progetti / workspace", color = Color.White) }, onClick = {
                            expanded = false
                            onAction("Lavora sul workspace/progetto selezionato e mostra piano prima di modificare file.")
                        })
                    }
                }
                TextField(
                    modifier = Modifier.weight(1f),
                    value = value,
                    onValueChange = onValueChange,
                    placeholder = { Text("Fai una domanda", color = AppColors.Muted) },
                    minLines = 1,
                    maxLines = 7,
                    colors = TextFieldDefaults.colors(
                        focusedContainerColor = Color.Transparent,
                        unfocusedContainerColor = Color.Transparent,
                        focusedIndicatorColor = Color.Transparent,
                        unfocusedIndicatorColor = Color.Transparent,
                        focusedTextColor = Color.White,
                        unfocusedTextColor = Color.White
                    )
                )
                Surface(color = Color.Transparent, shape = RoundedCornerShape(16.dp)) {
                    Text(
                        modifier = Modifier.padding(horizontal = 10.dp, vertical = 7.dp),
                        text = "$mode v",
                        color = AppColors.Muted,
                        fontSize = 12.sp
                    )
                }
                IconButton(onClick = {}) {
                    Text("mic", color = AppColors.Muted, fontSize = 12.sp)
                }
                Button(
                    modifier = Modifier.size(44.dp),
                    onClick = onSend,
                    colors = ButtonDefaults.buttonColors(containerColor = Color.White, contentColor = Color.Black),
                    shape = CircleShape,
                    contentPadding = androidx.compose.foundation.layout.PaddingValues(0.dp)
                ) {
                    Text(">", fontWeight = FontWeight.Bold)
                }
            }
        }
    }
}

@Composable
private fun PlaceholderScreen(title: String, subtitle: String, settings: AppSettings) {
    Column(
        modifier = Modifier
            .fillMaxSize()
            .padding(24.dp),
        verticalArrangement = Arrangement.Center,
        horizontalAlignment = Alignment.Start
    ) {
        Text(title, color = Color.White, fontSize = 30.sp, fontWeight = FontWeight.SemiBold)
        Spacer(modifier = Modifier.height(10.dp))
        Text(subtitle, color = AppColors.Muted)
        Spacer(modifier = Modifier.height(18.dp))
        Card(colors = CardDefaults.cardColors(containerColor = AppColors.Surface), shape = RoundedCornerShape(20.dp)) {
            Text(
                modifier = Modifier.padding(18.dp),
                text = "Gateway: ${settings.gatewayUrl}\nEndpoint NemoClaw lato server: ${settings.inferenceEndpoint}\nModello: ${settings.model}\nAccesso: ${settings.accessMode}",
                color = Color.White
            )
        }
    }
}

@Composable
private fun SettingsScreen(
    settings: AppSettings,
    onSave: (AppSettings) -> Unit,
    onReset: () -> Unit
) {
    var gatewayUrl by remember(settings) { mutableStateOf(settings.gatewayUrl) }
    var provider by remember(settings) { mutableStateOf(settings.provider) }
    var inferenceEndpoint by remember(settings) { mutableStateOf(settings.inferenceEndpoint) }
    var preferredApi by remember(settings) { mutableStateOf(settings.preferredApi) }
    var model by remember(settings) { mutableStateOf(settings.model) }
    var accessMode by remember(settings) { mutableStateOf(settings.accessMode) }
    var demoMode by remember(settings) { mutableStateOf(settings.demoMode) }

    Column(
        modifier = Modifier
            .fillMaxSize()
            .padding(24.dp),
        verticalArrangement = Arrangement.Center
    ) {
        Text("Impostazioni", color = Color.White, fontSize = 30.sp, fontWeight = FontWeight.SemiBold)
        Spacer(modifier = Modifier.height(10.dp))
        Text("Impostazioni salvate sul dispositivo. Segreti server fuori dal client.", color = AppColors.Muted)
        Spacer(modifier = Modifier.height(18.dp))
        LazyColumn(verticalArrangement = Arrangement.spacedBy(10.dp)) {
            item { SettingsField("Gateway URL", gatewayUrl, { gatewayUrl = it }) }
            item { SettingsField("Provider", provider, { provider = it }) }
            item { SettingsField("Inferenza lato server", inferenceEndpoint, { inferenceEndpoint = it }) }
            item { SettingsField("API preferita", preferredApi, { preferredApi = it }) }
            item { SettingsField("Modello", model, { model = it }) }
            item { SettingsField("Accesso", accessMode, { accessMode = it }) }
            item {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Text("Demo mode", color = Color.White, modifier = Modifier.weight(1f))
                    Switch(checked = demoMode, onCheckedChange = { demoMode = it })
                }
            }
            item {
                Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                    Button(onClick = {
                        onSave(
                            AppSettings(
                                gatewayUrl = gatewayUrl.trim(),
                                provider = provider.trim(),
                                inferenceEndpoint = inferenceEndpoint.trim(),
                                preferredApi = preferredApi.trim(),
                                model = model.trim(),
                                accessMode = accessMode.trim(),
                                demoMode = demoMode
                            )
                        )
                    }) {
                        Text("Salva")
                    }
                    Button(onClick = onReset) {
                        Text("Reset")
                    }
                }
            }
        }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun SettingsField(label: String, value: String, onValueChange: (String) -> Unit) {
    TextField(
        modifier = Modifier.fillMaxWidth(),
        value = value,
        onValueChange = onValueChange,
        label = { Text(label, color = AppColors.Muted) },
        colors = TextFieldDefaults.colors(
            focusedContainerColor = AppColors.Composer,
            unfocusedContainerColor = AppColors.Composer,
            focusedTextColor = Color.White,
            unfocusedTextColor = Color.White,
            focusedIndicatorColor = AppColors.Accent,
            unfocusedIndicatorColor = Color.Transparent
        )
    )
}

private fun appendPrompt(current: String, addition: String): String {
    val trimmed = current.trim()
    return if (trimmed.isEmpty()) addition else "$trimmed\n$addition"
}

private fun demoReply(settings: AppSettings, mode: String): String {
    val modeText = if (mode == "Agente") {
        "Creo task demo con approve/deny prima di file, rete, comandi e credenziali."
    } else {
        "Rispondo in chat demo senza avviare task agente."
    }

    return "$modeText Preset: gateway ${settings.gatewayUrl}, endpoint ${settings.inferenceEndpoint}, API ${settings.preferredApi}. Quando gateway sara' attivo useremo streaming reale."
}

private fun loadSettings(context: Context): AppSettings {
    val prefs = context.getSharedPreferences("nemoclaw_settings", Context.MODE_PRIVATE)
    return AppSettings(
        gatewayUrl = prefs.getString("gatewayUrl", AppDefaults.gatewayUrl) ?: AppDefaults.gatewayUrl,
        provider = prefs.getString("provider", AppDefaults.provider) ?: AppDefaults.provider,
        inferenceEndpoint = prefs.getString("inferenceEndpoint", AppDefaults.inferenceEndpoint) ?: AppDefaults.inferenceEndpoint,
        preferredApi = prefs.getString("preferredApi", AppDefaults.preferredApi) ?: AppDefaults.preferredApi,
        model = prefs.getString("model", AppDefaults.model) ?: AppDefaults.model,
        accessMode = prefs.getString("accessMode", AppDefaults.accessMode) ?: AppDefaults.accessMode,
        demoMode = prefs.getBoolean("demoMode", true)
    )
}

private fun saveSettings(context: Context, settings: AppSettings) {
    context.getSharedPreferences("nemoclaw_settings", Context.MODE_PRIVATE)
        .edit()
        .putString("gatewayUrl", settings.gatewayUrl)
        .putString("provider", settings.provider)
        .putString("inferenceEndpoint", settings.inferenceEndpoint)
        .putString("preferredApi", settings.preferredApi)
        .putString("model", settings.model)
        .putString("accessMode", settings.accessMode)
        .putBoolean("demoMode", settings.demoMode)
        .apply()
}

private object AppDefaults {
    const val gatewayUrl = "https://nemoclaw.local:8443"
    const val provider = "custom"
    const val inferenceEndpoint = "http://localhost:8000/v1"
    const val preferredApi = "openai-completions -> /v1/chat/completions"
    const val model = "meta-llama/Llama-3.1-8B-Instruct"
    const val accessMode = "VPN/LAN only"
}

private object AppColors {
    val Background = Color(0xFF212121)
    val Sidebar = Color(0xFF171717)
    val Composer = Color(0xFF303030)
    val Surface = Color(0xFF2B2B2B)
    val AssistantBubble = Color(0xFF282828)
    val UserBubble = Color(0xFF3A3A3A)
    val Muted = Color(0xFFB4B4B4)
    val Accent = Color(0xFF10A37F)
}
