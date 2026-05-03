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
import androidx.compose.foundation.layout.PaddingValues
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
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateListOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
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
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import java.net.HttpURLConnection
import java.net.URI
import java.net.URL
import org.json.JSONArray
import org.json.JSONObject

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
    Archive("Archivio"),
    Tasks("Ordini"),
    Server("Server"),
    Settings("Impostazioni")
}

private data class ChatMessage(
    val author: String,
    val text: String,
    val fromUser: Boolean,
    val isAction: Boolean = false
)

private data class AgentTask(
    val title: String,
    val mode: String,
    val status: String,
    val detail: String
)

private data class ArchiveItem(
    val id: String?,
    val title: String,
    val kind: String,
    val description: String,
    val prompt: String
)

private data class LocalConversation(
    val id: String,
    val title: String,
    val kind: String,
    val description: String,
    val prompt: String,
    val updatedAt: Long,
    val messages: List<ChatMessage>
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
    var pendingPrompt by remember { mutableStateOf("") }
    var pendingConversationId by remember { mutableStateOf<String?>(null) }

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
                Tab.Chat -> ChatScreen(
                    context = context,
                    settings = settings,
                    conversationId = pendingConversationId,
                    initialPrompt = pendingPrompt,
                    onInitialPromptConsumed = {
                        pendingPrompt = ""
                        pendingConversationId = null
                    }
                )
                Tab.Archive -> ArchiveScreen(
                    context = context,
                    onOpenConversation = { id, prompt ->
                        pendingConversationId = id
                        pendingPrompt = prompt
                        selectedTab = Tab.Chat
                    }
                )
                Tab.Tasks -> TasksScreen(settings)
                Tab.Server -> ServerScreen(settings)
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
private fun ChatScreen(
    context: Context,
    settings: AppSettings,
    conversationId: String? = null,
    initialPrompt: String = "",
    onInitialPromptConsumed: () -> Unit = {}
) {
    val messages = remember { mutableStateListOf<ChatMessage>() }
    var draft by remember { mutableStateOf("") }
    var mode by remember { mutableStateOf("Chat") }
    var activeConversationId by remember { mutableStateOf<String?>(null) }

    LaunchedEffect(conversationId, initialPrompt) {
        if (!conversationId.isNullOrBlank()) {
            val saved = loadConversation(context, conversationId)
            if (saved != null) {
                activeConversationId = saved.id
                messages.clear()
                messages.addAll(saved.messages)
            }
        }

        if (initialPrompt.isNotBlank()) {
            draft = initialPrompt
        }

        if (!conversationId.isNullOrBlank() || initialPrompt.isNotBlank()) {
            onInitialPromptConsumed()
        }
    }

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
                contentPadding = PaddingValues(18.dp, 24.dp),
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
            onAction = { title, text, prompt ->
                messages.add(ChatMessage(title, text, fromUser = false, isAction = true))
                if (prompt.isNotBlank()) {
                    draft = appendPrompt(draft, prompt)
                }
            },
            onModeChange = { mode = it },
            onSend = {
                val text = draft.trim()
                if (text.isNotEmpty()) {
                    val response = demoReply(settings, mode)
                    messages.add(ChatMessage("Tu", text, true))
                    messages.add(ChatMessage("NemoClaw", response, false))
                    activeConversationId = saveConversationExchange(context, activeConversationId, mode, text, response).id
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
                containerColor = when {
                    message.isAction -> AppColors.Surface
                    message.fromUser -> AppColors.UserBubble
                    else -> AppColors.AssistantBubble
                }
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
    onAction: (String, String, String) -> Unit,
    onModeChange: (String) -> Unit,
    onSend: () -> Unit
) {
    var expanded by remember { mutableStateOf(false) }
    fun queueAction(title: String, detail: String, prompt: String) {
        onAction(title, detail, prompt)
    }

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
                        DropdownMenuItem(text = { Text("[file]  Aggiungi file al task", color = Color.White) }, onClick = {
                            expanded = false
                            queueAction("File task", "File picker Android da collegare: prepara il task con allegato e conferma utente.", "Allega un file al prossimo task e analizzalo nel contesto NemoClaw.")
                        })
                        DropdownMenuItem(text = { Text("[shot]  Cattura screenshot", color = Color.White) }, onClick = {
                            expanded = false
                            queueAction("Screenshot", "Cattura schermo da collegare con permesso Android; per ora aggiungo richiesta al prompt.", "Usa uno screenshot come contesto visivo per capire app o server.")
                        })
                        DropdownMenuItem(text = { Text("[cam]  Scatta foto", color = Color.White) }, onClick = {
                            expanded = false
                            queueAction("Foto", "Camera picker Android da collegare: azione pronta per allegato immagine.", "Acquisisci una foto e usala come allegato per la conversazione.")
                        })
                        DropdownMenuItem(text = { Text("[chat]  Modalita: Chat", color = Color.White) }, onClick = {
                            expanded = false
                            onModeChange("Chat")
                            onAction("Modalita", "Chat attiva: messaggi normali, nessun task agente automatico.", "")
                        })
                        DropdownMenuItem(text = { Text("[agent]  Modalita: Agente", color = Color.White) }, onClick = {
                            expanded = false
                            onModeChange("Agente")
                            onAction("Modalita", "Agente attivo: task demo con approve/deny per azioni rischiose.", "")
                        })
                        DropdownMenuItem(text = { Text("[image]  Crea immagine", color = Color.White) }, onClick = {
                            expanded = false
                            queueAction("Immagine", "Generazione immagine richiedera' gateway/tool dedicato e conferma prima di chiamate esterne.", "Prepara una richiesta di generazione immagine, ma chiedi conferma prima di usare tool esterni.")
                        })
                        DropdownMenuItem(text = { Text("[research]  Deep Research locale", color = Color.White) }, onClick = {
                            expanded = false
                            queueAction("Deep Research", "Ricerca approfondita locale; rete solo dopo approvazione esplicita.", "Esegui una ricerca approfondita e cita fonti, usando rete solo dopo approvazione.")
                        })
                        DropdownMenuItem(text = { Text("[web]  Ricerca web autorizzata", color = Color.White) }, onClick = {
                            expanded = false
                            queueAction("Web", "Ricerca web marcata come azione autorizzabile: nessuna rete fuori LAN/VPN senza conferma.", "Cerca sul web informazioni aggiornate, chiedendo conferma prima di uscire dalla LAN/VPN.")
                        })
                        DropdownMenuItem(text = { Text("[project]  Progetti / workspace", color = Color.White) }, onClick = {
                            expanded = false
                            queueAction("Workspace", "Workspace/progetti saranno collegati al gateway task con audit trail.", "Lavora sul workspace/progetto selezionato e mostra piano prima di modificare file.")
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
                    contentPadding = PaddingValues(0.dp)
                ) {
                    Text(">", fontWeight = FontWeight.Bold)
                }
            }
        }
    }
}

@Composable
private fun ArchiveScreen(
    context: Context,
    onOpenConversation: (String?, String) -> Unit
) {
    var refreshKey by remember { mutableStateOf(0) }
    val archive = remember(refreshKey) { loadArchiveItems(context) }
    var query by remember { mutableStateOf("") }
    var filter by remember { mutableStateOf("Tutto") }
    var status by remember { mutableStateOf("Pronto.") }
    val results = archive.filter { item ->
        (filter == "Tutto" || item.kind == filter) &&
            (query.isBlank() ||
                item.title.contains(query, ignoreCase = true) ||
                item.description.contains(query, ignoreCase = true) ||
                item.prompt.contains(query, ignoreCase = true))
    }

    LazyColumn(
        modifier = Modifier
            .fillMaxSize()
            .padding(24.dp),
        verticalArrangement = Arrangement.spacedBy(14.dp)
    ) {
        item {
            Text("Archivio", color = Color.White, fontSize = 30.sp, fontWeight = FontWeight.SemiBold)
            Spacer(modifier = Modifier.height(8.dp))
            Text("Ricerca locale demo per chat, progetti e task recenti.", color = AppColors.Muted)
        }
        item {
            Card(colors = CardDefaults.cardColors(containerColor = AppColors.Surface), shape = RoundedCornerShape(20.dp)) {
                Column(modifier = Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
                    SettingsField("Cerca", query, { query = it })
                    Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                        listOf("Tutto", "Chat", "Progetto", "Task", "Server").forEach { option ->
                            Button(
                                onClick = {
                                    filter = option
                                    status = "Filtro: $option"
                                },
                                colors = ButtonDefaults.buttonColors(
                                    containerColor = if (filter == option) AppColors.Accent else AppColors.AssistantBubble,
                                    contentColor = Color.White
                                )
                            ) {
                                Text(option, fontSize = 12.sp)
                            }
                        }
                    }
                    Text(status, color = AppColors.Muted)
                }
            }
        }
        if (results.isEmpty()) {
            item {
                Text("Nessun risultato.", color = AppColors.Muted)
            }
        }
        items(results) { item ->
            ArchiveCard(
                item = item,
                onOpen = {
                    status = "Prompt aperto in chat: ${item.title}"
                    onOpenConversation(item.id, item.prompt)
                },
                onPin = {
                    val saved = saveProjectConversation(context, item.title, item.description, item.prompt)
                    status = "Progetto salvato localmente: ${saved.title}"
                    refreshKey++
                }
            )
        }
    }
}

@Composable
private fun ArchiveCard(
    item: ArchiveItem,
    onOpen: () -> Unit,
    onPin: () -> Unit
) {
    Card(colors = CardDefaults.cardColors(containerColor = AppColors.AssistantBubble), shape = RoundedCornerShape(20.dp)) {
        Column(modifier = Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Text(item.title, color = Color.White, fontWeight = FontWeight.SemiBold, modifier = Modifier.weight(1f))
                Text(item.kind, color = AppColors.Accent, fontSize = 12.sp)
            }
            Text(item.description, color = AppColors.Muted)
            Text(item.prompt, color = Color.White, fontSize = 13.sp)
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                Button(onClick = onOpen) { Text("Apri") }
                Button(onClick = onPin) { Text("Segna") }
            }
        }
    }
}

@Composable
private fun TasksScreen(settings: AppSettings) {
    val tasks = remember {
        mutableStateListOf(
            AgentTask(
                title = "Controllo gateway NemoClaw",
                mode = "Health",
                status = "In attesa",
                detail = "Verifica /api/health e modello locale prima di eseguire task agente."
            )
        )
    }
    var title by remember { mutableStateOf("") }
    var detail by remember { mutableStateOf("") }
    var status by remember { mutableStateOf("Pronto.") }

    LazyColumn(
        modifier = Modifier
            .fillMaxSize()
            .padding(24.dp),
        verticalArrangement = Arrangement.spacedBy(14.dp)
    ) {
        item {
            Text("Ordini agente", color = Color.White, fontSize = 30.sp, fontWeight = FontWeight.SemiBold)
            Spacer(modifier = Modifier.height(8.dp))
            Text("Coda locale task con approve/deny. Gateway futuro: /api/tasks e /api/tasks/{id}/events.", color = AppColors.Muted)
        }
        item {
            Card(colors = CardDefaults.cardColors(containerColor = AppColors.Surface), shape = RoundedCornerShape(20.dp)) {
                Column(modifier = Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
                    Text("Nuovo ordine", color = Color.White, fontWeight = FontWeight.SemiBold)
                    SettingsField("Titolo", title, { title = it })
                    SettingsField("Istruzioni", detail, { detail = it })
                    Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                        Button(onClick = {
                            if (title.isBlank()) {
                                status = "Titolo obbligatorio."
                                return@Button
                            }
                            tasks.add(
                                0,
                                AgentTask(
                                    title = title.trim(),
                                    mode = if (settings.demoMode) "Demo" else "Gateway",
                                    status = "In attesa approvazione",
                                    detail = detail.ifBlank { "Esegui con piano prima di azioni rischiose." }.trim()
                                )
                            )
                            title = ""
                            detail = ""
                            status = "Task accodato."
                        }) {
                            Text("Accoda")
                        }
                        Button(onClick = {
                            title = "Analizza workspace"
                            detail = "Mostra piano, poi chiedi approve prima di leggere/modificare file."
                            status = "Template workspace caricato."
                        }) {
                            Text("Template")
                        }
                    }
                    Text(status, color = AppColors.Muted)
                }
            }
        }
        items(tasks) { task ->
            TaskCard(
                task = task,
                onApprove = {
                    val index = tasks.indexOf(task)
                    if (index >= 0) tasks[index] = task.copy(status = "Approvato")
                },
                onDeny = {
                    val index = tasks.indexOf(task)
                    if (index >= 0) tasks[index] = task.copy(status = "Negato")
                },
                onDone = {
                    val index = tasks.indexOf(task)
                    if (index >= 0) tasks[index] = task.copy(status = "Completato demo")
                }
            )
        }
    }
}

@Composable
private fun TaskCard(
    task: AgentTask,
    onApprove: () -> Unit,
    onDeny: () -> Unit,
    onDone: () -> Unit
) {
    Card(colors = CardDefaults.cardColors(containerColor = AppColors.AssistantBubble), shape = RoundedCornerShape(20.dp)) {
        Column(modifier = Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Text(task.title, color = Color.White, fontWeight = FontWeight.SemiBold, modifier = Modifier.weight(1f))
                Text(task.status, color = AppColors.Accent, fontSize = 12.sp)
            }
            Text("Modalita: ${task.mode}", color = AppColors.Muted, fontSize = 12.sp)
            Text(task.detail, color = Color.White)
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                Button(onClick = onApprove) { Text("Approva") }
                Button(onClick = onDeny) { Text("Nega") }
                Button(onClick = onDone) { Text("Completa") }
            }
        }
    }
}

@Composable
private fun ServerScreen(settings: AppSettings) {
    val scope = rememberCoroutineScope()
    var status by remember(settings) {
        mutableStateOf(if (settings.demoMode) "Demo mode attivo. Nessuna chiamata automatica al server." else "Pronto per test gateway.")
    }

    LazyColumn(
        modifier = Modifier
            .fillMaxSize()
            .padding(24.dp),
        verticalArrangement = Arrangement.spacedBy(14.dp)
    ) {
        item {
            Text("Server", color = Color.White, fontSize = 30.sp, fontWeight = FontWeight.SemiBold)
            Spacer(modifier = Modifier.height(8.dp))
            Text("Dashboard gateway, modello locale, sandbox e policy rete.", color = AppColors.Muted)
        }
        item {
            ServerMetric("Gateway", settings.gatewayUrl, "Health endpoint: ${settings.gatewayUrl.trimEnd('/')}/api/health")
        }
        item {
            ServerMetric("Modello", settings.model, "Provider: ${settings.provider} | API: ${settings.preferredApi}")
        }
        item {
            ServerMetric("Inferenza lato server", settings.inferenceEndpoint, "Il client non parla direttamente con Ollama/local inference.")
        }
        item {
            ServerMetric("Sicurezza", settings.accessMode, "Deny by default, segreti solo lato server, rete esterna dopo approve.")
        }
        item {
            Card(colors = CardDefaults.cardColors(containerColor = AppColors.Surface), shape = RoundedCornerShape(20.dp)) {
                Column(modifier = Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
                    Text("Azioni", color = Color.White, fontWeight = FontWeight.SemiBold)
                    Text(status, color = AppColors.Muted)
                    Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                        Button(onClick = {
                            val error = validateHttpUrl(settings.gatewayUrl, "Gateway URL")
                            if (error != null) {
                                status = error
                                return@Button
                            }
                            val healthUrl = "${settings.gatewayUrl.trimEnd('/')}/api/health"
                            status = "Test: $healthUrl"
                            scope.launch {
                                status = testGateway(healthUrl)
                            }
                        }) {
                            Text("Test gateway")
                        }
                        Button(onClick = {
                            status = "Contratto atteso: GET /api/health, GET /api/server/status, POST /api/chat/stream, POST /api/tasks."
                        }) {
                            Text("Mostra API")
                        }
                    }
                }
            }
        }
    }
}

@Composable
private fun ServerMetric(title: String, value: String, detail: String) {
    Card(colors = CardDefaults.cardColors(containerColor = AppColors.Surface), shape = RoundedCornerShape(20.dp)) {
        Column(modifier = Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
            Text(title, color = Color.White, fontWeight = FontWeight.SemiBold)
            Text(value, color = Color.White)
            Text(detail, color = AppColors.Muted, fontSize = 12.sp)
        }
    }
}

@Composable
private fun SettingsScreen(
    settings: AppSettings,
    onSave: (AppSettings) -> Unit,
    onReset: () -> Unit
) {
    val scope = rememberCoroutineScope()
    var gatewayUrl by remember(settings) { mutableStateOf(settings.gatewayUrl) }
    var provider by remember(settings) { mutableStateOf(settings.provider) }
    var inferenceEndpoint by remember(settings) { mutableStateOf(settings.inferenceEndpoint) }
    var preferredApi by remember(settings) { mutableStateOf(settings.preferredApi) }
    var model by remember(settings) { mutableStateOf(settings.model) }
    var accessMode by remember(settings) { mutableStateOf(settings.accessMode) }
    var demoMode by remember(settings) { mutableStateOf(settings.demoMode) }
    var status by remember(settings) { mutableStateOf("Pronto.") }

    Column(
        modifier = Modifier
            .fillMaxSize()
            .padding(24.dp),
        verticalArrangement = Arrangement.Top
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
                Surface(color = AppColors.Surface, shape = RoundedCornerShape(16.dp)) {
                    Text(
                        modifier = Modifier.padding(14.dp),
                        text = status,
                        color = AppColors.Muted
                    )
                }
            }
            item {
                Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                    Button(onClick = {
                        val candidate = AppSettings(
                            gatewayUrl = gatewayUrl.trim(),
                            provider = provider.trim(),
                            inferenceEndpoint = inferenceEndpoint.trim(),
                            preferredApi = preferredApi.trim(),
                            model = model.trim(),
                            accessMode = accessMode.trim(),
                            demoMode = demoMode
                        )
                        val error = validateSettings(candidate)
                        if (error == null) {
                            onSave(candidate)
                            status = "Impostazioni salvate. Pairing code non salvato per sicurezza."
                        } else {
                            status = error
                        }
                    }) {
                        Text("Salva")
                    }
                    Button(onClick = {
                        val error = validateHttpUrl(gatewayUrl, "Gateway URL")
                        if (error != null) {
                            status = error
                            return@Button
                        }

                        val healthUrl = "${gatewayUrl.trimEnd('/')}/api/health"
                        status = "Test: $healthUrl"
                        scope.launch {
                            status = testGateway(healthUrl)
                        }
                    }) {
                        Text("Test")
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

private fun validateSettings(settings: AppSettings): String? {
    return validateHttpUrl(settings.gatewayUrl, "Gateway URL")
        ?: validateRequired(settings.provider, "Provider")
        ?: validateHttpUrl(settings.inferenceEndpoint, "Endpoint inferenza")
        ?: validateRequired(settings.preferredApi, "API preferita")
        ?: validateRequired(settings.model, "Modello")
        ?: validateRequired(settings.accessMode, "Accesso")
}

private fun validateHttpUrl(value: String, label: String): String? {
    if (value.isBlank()) return "$label obbligatorio."

    return try {
        val uri = URI(value)
        val scheme = uri.scheme.orEmpty().lowercase()
        if ((scheme == "http" || scheme == "https") && uri.host != null) null else "$label deve essere URL http/https valido."
    } catch (_: Exception) {
        "$label deve essere URL http/https valido."
    }
}

private fun validateRequired(value: String, label: String): String? {
    return if (value.isBlank()) "$label obbligatorio." else null
}

private suspend fun testGateway(healthUrl: String): String = withContext(Dispatchers.IO) {
    try {
        val connection = (URL(healthUrl).openConnection() as HttpURLConnection).apply {
            requestMethod = "GET"
            connectTimeout = 5_000
            readTimeout = 5_000
        }

        connection.use {
            val code = it.responseCode
            if (code in 200..299) "Gateway raggiungibile." else "Gateway risponde: HTTP $code"
        }
    } catch (ex: Exception) {
        "Gateway non raggiungibile: ${ex.message ?: ex.javaClass.simpleName}"
    }
}

private inline fun <T : HttpURLConnection, R> T.use(block: (T) -> R): R {
    return try {
        block(this)
    } finally {
        disconnect()
    }
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

private fun loadArchiveItems(context: Context): List<ArchiveItem> {
    val saved = loadConversations(context).map {
        ArchiveItem(
            id = it.id,
            title = it.title,
            kind = it.kind,
            description = it.description.ifBlank { "Ultimo aggiornamento locale." },
            prompt = it.prompt
        )
    }
    val seeds = listOf(
        ArchiveItem(null, "Setup gateway NemoClaw", "Progetto", "Piano per gateway locale, TLS LAN/VPN e endpoint OpenAI-compatible.", "Preparami i passaggi per avviare NemoClaw con gateway locale."),
        ArchiveItem(null, "Test modello locale", "Chat", "Conversazione demo per verificare modello, API e streaming futuro.", "Testa risposta modello locale con prompt breve."),
        ArchiveItem(null, "Controllo home-server", "Server", "Snapshot gateway, modello, sandbox e policy rete.", "Controlla stato gateway, modello locale e sandbox NemoClaw."),
        ArchiveItem(null, "Analizza workspace", "Task", "Task agente con approve prima di leggere o modificare file.", "Analizza workspace, mostra piano e chiedi approve prima di modificare."),
        ArchiveItem(null, "Ricerca web autorizzata", "Task", "Rete esterna solo dopo conferma esplicita.", "Cerca informazioni aggiornate, ma chiedi conferma prima di uscire dalla LAN/VPN."),
        ArchiveItem(null, "Documenti progetto", "Progetto", "Guide Windows/Android e memoria AGENTS.md.", "Riassumi stato progetto e prossimi passi.")
    )

    return saved + seeds.filter { seed -> saved.none { it.title.equals(seed.title, ignoreCase = true) } }
}

private fun loadConversation(context: Context, id: String): LocalConversation? {
    return loadConversations(context).firstOrNull { it.id == id }
}

private fun saveConversationExchange(
    context: Context,
    conversationId: String?,
    mode: String,
    prompt: String,
    response: String
): LocalConversation {
    val conversations = loadConversations(context).toMutableList()
    val index = conversations.indexOfFirst { it.id == conversationId }
    val now = System.currentTimeMillis()
    val newMessages = listOf(
        ChatMessage("Tu", prompt, fromUser = true),
        ChatMessage("NemoClaw", response, fromUser = false)
    )

    val conversation = if (index >= 0) {
        val current = conversations[index]
        current.copy(
            kind = if (mode == "Agente") "Task" else current.kind,
            prompt = prompt,
            updatedAt = now,
            messages = current.messages + newMessages
        )
    } else {
        LocalConversation(
            id = "conv_$now",
            title = makeTitle(prompt),
            kind = if (mode == "Agente") "Task" else "Chat",
            description = if (mode == "Agente") "Conversazione agente con approve/deny demo." else "Conversazione chat locale.",
            prompt = prompt,
            updatedAt = now,
            messages = newMessages
        )
    }

    if (index >= 0) {
        conversations[index] = conversation
    } else {
        conversations.add(0, conversation)
    }
    saveConversations(context, conversations)
    return conversation
}

private fun saveProjectConversation(
    context: Context,
    title: String,
    description: String,
    prompt: String
): LocalConversation {
    val conversations = loadConversations(context).toMutableList()
    val now = System.currentTimeMillis()
    val index = conversations.indexOfFirst { it.kind == "Progetto" && it.title.equals(title, ignoreCase = true) }
    val project = if (index >= 0) {
        conversations[index].copy(description = description, prompt = prompt, updatedAt = now)
    } else {
        LocalConversation(
            id = "project_$now",
            title = title,
            kind = "Progetto",
            description = description,
            prompt = prompt,
            updatedAt = now,
            messages = emptyList()
        )
    }

    if (index >= 0) {
        conversations[index] = project
    } else {
        conversations.add(0, project)
    }
    saveConversations(context, conversations)
    return project
}

private fun loadConversations(context: Context): List<LocalConversation> {
    val raw = context.getSharedPreferences("nemoclaw_archive", Context.MODE_PRIVATE).getString("items", "[]") ?: "[]"
    return try {
        val array = JSONArray(raw)
        buildList {
            for (i in 0 until array.length()) {
                val obj = array.getJSONObject(i)
                add(
                    LocalConversation(
                        id = obj.optString("id"),
                        title = obj.optString("title", "Nuova chat"),
                        kind = obj.optString("kind", "Chat"),
                        description = obj.optString("description"),
                        prompt = obj.optString("prompt"),
                        updatedAt = obj.optLong("updatedAt"),
                        messages = readMessages(obj.optJSONArray("messages") ?: JSONArray())
                    )
                )
            }
        }.sortedByDescending { it.updatedAt }
    } catch (_: Exception) {
        emptyList()
    }
}

private fun saveConversations(context: Context, conversations: List<LocalConversation>) {
    val array = JSONArray()
    conversations
        .sortedByDescending { it.updatedAt }
        .take(200)
        .forEach { conversation ->
            array.put(
                JSONObject()
                    .put("id", conversation.id)
                    .put("title", conversation.title)
                    .put("kind", conversation.kind)
                    .put("description", conversation.description)
                    .put("prompt", conversation.prompt)
                    .put("updatedAt", conversation.updatedAt)
                    .put("messages", writeMessages(conversation.messages))
            )
        }

    context.getSharedPreferences("nemoclaw_archive", Context.MODE_PRIVATE)
        .edit()
        .putString("items", array.toString())
        .apply()
}

private fun readMessages(array: JSONArray): List<ChatMessage> {
    return buildList {
        for (i in 0 until array.length()) {
            val obj = array.getJSONObject(i)
            add(
                ChatMessage(
                    author = obj.optString("author"),
                    text = obj.optString("text"),
                    fromUser = obj.optBoolean("fromUser"),
                    isAction = false
                )
            )
        }
    }
}

private fun writeMessages(messages: List<ChatMessage>): JSONArray {
    val array = JSONArray()
    messages.forEach { message ->
        if (!message.isAction) {
            array.put(
                JSONObject()
                    .put("author", message.author)
                    .put("text", message.text)
                    .put("fromUser", message.fromUser)
            )
        }
    }
    return array
}

private fun makeTitle(prompt: String): String {
    val oneLine = prompt.lines().joinToString(" ").trim()
    return if (oneLine.length <= 46) oneLine else oneLine.take(46).trimEnd() + "..."
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
