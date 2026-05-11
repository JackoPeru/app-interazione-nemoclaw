package com.nemoclaw.chat

import android.content.Context
import android.content.ClipData
import android.content.ClipboardManager
import android.content.Intent
import android.content.SharedPreferences
import android.net.Uri
import android.os.Bundle
import android.provider.MediaStore
import android.provider.Settings
import android.security.keystore.KeyGenParameterSpec
import android.security.keystore.KeyProperties
import android.speech.RecognizerIntent
import android.util.Base64
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.ExperimentalLayoutApi
import androidx.compose.foundation.layout.FlowRow
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
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.rounded.ManageSearch
import androidx.compose.material.icons.rounded.AccountCircle
import androidx.compose.material.icons.rounded.Add
import androidx.compose.material.icons.rounded.ArrowUpward
import androidx.compose.material.icons.rounded.AttachFile
import androidx.compose.material.icons.rounded.ChatBubbleOutline
import androidx.compose.material.icons.rounded.CropFree
import androidx.compose.material.icons.rounded.Delete
import androidx.compose.material.icons.rounded.Dns
import androidx.compose.material.icons.rounded.FolderOpen
import androidx.compose.material.icons.rounded.Image
import androidx.compose.material.icons.rounded.Language
import androidx.compose.material.icons.rounded.ManageSearch
import androidx.compose.material.icons.rounded.Mic
import androidx.compose.material.icons.rounded.PhotoCamera
import androidx.compose.material.icons.rounded.SmartToy
import androidx.compose.material.icons.rounded.TaskAlt
import androidx.compose.material.icons.rounded.Terminal
import androidx.compose.material.icons.rounded.Tune
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.DropdownMenu
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.LinearProgressIndicator
import androidx.compose.material3.NavigationBar
import androidx.compose.material3.NavigationBarItem
import androidx.compose.material3.NavigationBarItemDefaults
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
import androidx.compose.ui.draw.clip
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.core.content.FileProvider
import com.nemoclaw.chat.ui.theme.ChatClawTheme
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import kotlinx.coroutines.withTimeout
import okhttp3.OkHttpClient
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody
import okhttp3.Response
import okhttp3.WebSocket
import okhttp3.WebSocketListener
import java.io.File
import java.net.HttpURLConnection
import java.net.URI
import java.net.URL
import java.security.KeyStore
import java.util.UUID
import javax.crypto.Cipher
import javax.crypto.KeyGenerator
import javax.crypto.SecretKey
import javax.crypto.spec.GCMParameterSpec
import org.json.JSONArray
import org.json.JSONObject

class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContent {
            ChatClawTheme {
                ChatApp()
            }
        }
    }
}

private enum class Tab(val label: String, val icon: ImageVector) {
    Chat("Chat", Icons.Rounded.ChatBubbleOutline),
    Archive("Archivio", Icons.Rounded.FolderOpen),
    Tasks("Jobs", Icons.Rounded.TaskAlt),
    Server("Hermes", Icons.Rounded.Dns),
    Operator("Runs", Icons.Rounded.Terminal),
    Settings("Imposta", Icons.Rounded.Tune),
    Profile("Profilo", Icons.Rounded.AccountCircle)
}

data class ChatMessage(
    val author: String,
    val text: String,
    val fromUser: Boolean,
    val isAction: Boolean = false
)

data class AgentTask(
    val id: String,
    val remoteId: String? = null,
    val title: String,
    val mode: String,
    val status: String,
    val detail: String,
    val requiresApproval: Boolean = true,
    val source: String = "Locale",
    val updatedAt: Long = System.currentTimeMillis()
)

private data class ArchiveItem(
    val id: String?,
    val title: String,
    val kind: String,
    val description: String,
    val prompt: String
)

data class LocalConversation(
    val id: String,
    val title: String,
    val kind: String,
    val description: String,
    val prompt: String,
    val updatedAt: Long,
    val messages: List<ChatMessage>,
    val previousResponseId: String? = null
)

private data class UpdateCheckResult(
    val hasUpdate: Boolean,
    val latestVersion: String?,
    val message: String,
    val releaseUrl: String,
    val assetUrl: String?
)

private data class UpdateDownloadState(
    val status: String = "Controlla GitHub Releases per nuove versioni.",
    val releaseAssetUrl: String? = null,
    val latestVersion: String? = null,
    val hasUpdate: Boolean = false,
    val isDownloading: Boolean = false,
    val progress: Float? = null,
    val downloadLabel: String = "",
    val downloadedApkPath: String? = null
)

data class AppSettings(
    val gatewayUrl: String = AppDefaults.gatewayUrl,
    val gatewayWsUrl: String = AppDefaults.gatewayWsUrl,
    val adminBridgeUrl: String = AppDefaults.adminBridgeUrl,
    val provider: String = AppDefaults.provider,
    val inferenceEndpoint: String = AppDefaults.inferenceEndpoint,
    val preferredApi: String = AppDefaults.preferredApi,
    val model: String = AppDefaults.model,
    val accessMode: String = AppDefaults.accessMode,
    val demoMode: Boolean = true
)

private data class GatewayChatResult(
    val text: String,
    val source: String,
    val statusMessage: String,
    val usedFallback: Boolean,
    val responseId: String? = null
)

private data class GatewayTaskResult(
    val task: AgentTask,
    val message: String
)

private data class ServerSnapshot(
    val gateway: String,
    val model: String,
    val providerDetail: String,
    val inferenceEndpoint: String,
    val policy: String,
    val statusMessage: String
)

private data class GatewayWsProbe(
    val wsUrl: String,
    val connected: Boolean,
    val status: String,
    val detail: String,
    val capabilityLines: List<String> = emptyList()
)

private data class GatewayRpcCallResult(
    val method: String,
    val success: Boolean,
    val status: String,
    val rawJson: String,
    val summary: String
)

private data class OperatorPreset(
    val group: String,
    val label: String,
    val method: String,
    val params: String
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
                        colors = NavigationBarItemDefaults.colors(
                            selectedIconColor = Color.White,
                            selectedTextColor = Color.White,
                            indicatorColor = AppColors.NavIndicator,
                            unselectedIconColor = AppColors.Muted,
                            unselectedTextColor = AppColors.Muted
                        ),
                        icon = { Icon(tab.icon, contentDescription = tab.label) },
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
                Tab.Tasks -> TasksScreen(context, settings)
                Tab.Server -> ServerScreen(context, settings)
                Tab.Operator -> OperatorScreen(context, settings)
                Tab.Settings -> SettingsScreen(
                    settings = settings,
                    onSave = {
                        settings = it
                        saveSettings(context, it)
                    },
                    onReset = {
                        settings = AppSettings()
                        saveSettings(context, settings)
                        deleteGatewaySecret(context)
                    }
                )
                Tab.Profile -> ProfileScreen(context, settings)
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
    val scope = rememberCoroutineScope()
    var draft by remember { mutableStateOf("") }
    var mode by remember { mutableStateOf("Chat") }
    var activeConversationId by remember { mutableStateOf<String?>(null) }
    var previousResponseId by remember { mutableStateOf<String?>(null) }
    var sending by remember { mutableStateOf(false) }

    LaunchedEffect(conversationId, initialPrompt) {
        if (!conversationId.isNullOrBlank()) {
            val saved = loadConversation(context, conversationId)
            if (saved != null) {
                activeConversationId = saved.id
                previousResponseId = saved.previousResponseId
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
        TopBar(
            mode = mode,
            onModeToggle = {
                mode = if (mode == "Agente") "Chat" else "Agente"
            }
        )
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
            context = context,
            value = draft,
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
                if (text.isNotEmpty() && !sending) {
                    messages.add(ChatMessage("Tu", text, true))
                    draft = ""
                    sending = true

                    scope.launch {
                        val result = sendChatRequest(settings, mode, text, messages, activeConversationId, previousResponseId, loadGatewaySecret(context))
                        messages.add(ChatMessage("Hermes", result.text, false))
                        if (result.usedFallback) {
                            messages.add(ChatMessage("Stato", result.statusMessage, fromUser = false, isAction = true))
                        }
                        val saved = saveConversationExchange(
                            context,
                            activeConversationId,
                            mode,
                            text,
                            result.text,
                            result.source,
                            result.responseId
                        )
                        activeConversationId = saved.id
                        previousResponseId = saved.previousResponseId
                        sending = false
                    }
                }
            },
            isBusy = sending
        )
    }
}

@Composable
private fun TopBar(mode: String, onModeToggle: () -> Unit) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .height(72.dp)
            .padding(horizontal = 18.dp, vertical = 8.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        Image(
            painter = painterResource(id = R.drawable.chatclaw_logo),
            contentDescription = "Hermes Hub",
            contentScale = ContentScale.Crop,
            modifier = Modifier
                .size(52.dp)
                .clip(RoundedCornerShape(16.dp))
        )
        Column(modifier = Modifier.padding(start = 12.dp)) {
            Text(
                text = "Hermes Hub",
                color = Color.White,
                fontWeight = FontWeight.SemiBold,
                fontSize = 20.sp
            )
        }
        Spacer(modifier = Modifier.weight(1f))
        val modeIcon = if (mode == "Agente") Icons.Rounded.SmartToy else Icons.Rounded.ChatBubbleOutline
        Surface(
            modifier = Modifier.clickable(onClick = onModeToggle),
            color = AppColors.Surface,
            shape = RoundedCornerShape(18.dp)
        ) {
            Row(
                modifier = Modifier.padding(horizontal = 12.dp, vertical = 7.dp),
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.spacedBy(8.dp)
            ) {
                Icon(modeIcon, contentDescription = mode, tint = AppColors.Accent, modifier = Modifier.size(16.dp))
                Text(text = mode, color = Color.White, fontSize = 12.sp, fontWeight = FontWeight.Medium)
            }
        }
    }
}

@Composable
private fun EmptyState(onPrompt: (String) -> Unit) {
    Column(
        modifier = Modifier
            .fillMaxSize()
            .background(
                Brush.verticalGradient(
                    listOf(Color(0x1AF5A524), AppColors.Background, AppColors.Background)
                )
            )
            .padding(horizontal = 24.dp),
        verticalArrangement = Arrangement.Center,
        horizontalAlignment = Alignment.CenterHorizontally
    ) {
        Image(
            painter = painterResource(id = R.drawable.chatclaw_logo),
            contentDescription = "Logo Hermes Hub",
            contentScale = ContentScale.Crop,
            modifier = Modifier
                .size(108.dp)
                .clip(RoundedCornerShape(28.dp))
        )
        Spacer(modifier = Modifier.height(18.dp))
        Text(
            text = "Che vuoi fare oggi, Matteo?",
            color = Color.White,
            fontWeight = FontWeight.SemiBold,
            fontSize = 24.sp
        )
        Spacer(modifier = Modifier.height(10.dp))
        Text(
            text = "Chat normale, Runs e Jobs verso Hermes Agent sul tuo home-server.",
            color = AppColors.Muted,
            fontSize = 14.sp,
            textAlign = TextAlign.Center
        )
        Spacer(modifier = Modifier.height(18.dp))
        Column(verticalArrangement = Arrangement.spacedBy(10.dp)) {
            SuggestionButton("Prepara setup Hermes") {
                onPrompt("Preparami i passaggi per avviare Hermes Agent API Server su Tailscale/LAN.")
            }
            SuggestionButton("Controlla server") {
                onPrompt("Controlla stato Hermes, modello disponibile e capabilities API.")
            }
            SuggestionButton("Crea ordine agente") {
                onPrompt("Crea un task agente sicuro con richiesta approve/deny prima di ogni azione rischiosa.")
            }
        }
    }
}

private fun createInitialTasks(@Suppress("UNUSED_PARAMETER") settings: AppSettings): List<AgentTask> {
    return emptyList()
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
    context: Context,
    value: String,
    onValueChange: (String) -> Unit,
    onAction: (String, String, String) -> Unit,
    onModeChange: (String) -> Unit,
    onSend: () -> Unit,
    isBusy: Boolean
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
        Column(modifier = Modifier.padding(horizontal = 10.dp, vertical = 8.dp)) {
            TextField(
                modifier = Modifier.fillMaxWidth(),
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
                    unfocusedTextColor = Color.White,
                    cursorColor = AppColors.Accent
                )
            )
            Row(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(top = 4.dp),
                verticalAlignment = Alignment.CenterVertically
            ) {
                Box {
                    IconButton(onClick = { expanded = true }) {
                        Icon(Icons.Rounded.Add, contentDescription = "Apri menu azioni", tint = AppColors.Muted)
                    }
                    DropdownMenu(
                        expanded = expanded,
                        onDismissRequest = { expanded = false },
                        containerColor = AppColors.Elevated
                    ) {
                        DropdownMenuItem(
                            text = { Text("Aggiungi file al task", color = Color.White) },
                            leadingIcon = { Icon(Icons.Rounded.AttachFile, null, tint = Color.White) },
                            onClick = {
                                expanded = false
                                val opened = openAndroidIntent(
                                    context,
                                    Intent(Intent.ACTION_OPEN_DOCUMENT).apply {
                                        addCategory(Intent.CATEGORY_OPENABLE)
                                        type = "*/*"
                                    }
                                )
                                queueAction(
                                    "File task",
                                    if (opened) "File picker Android aperto. Seleziona il file e descrivilo nel prompt del task." else "Nessun file picker disponibile. Descrivi percorso o contenuto del file nel prompt.",
                                    "Allega un file al prossimo job e analizzalo nel contesto Hermes."
                                )
                            }
                        )
                        DropdownMenuItem(
                            text = { Text("Cattura screenshot", color = Color.White) },
                            leadingIcon = { Icon(Icons.Rounded.CropFree, null, tint = Color.White) },
                            onClick = {
                                expanded = false
                                queueAction(
                                    "Screenshot",
                                    "Richiesta screenshot aggiunta. Usa la cattura schermo del telefono, poi allega l'immagine dal menu file.",
                                    "Usa uno screenshot come contesto visivo per capire app o server."
                                )
                            }
                        )
                        DropdownMenuItem(
                            text = { Text("Scatta foto", color = Color.White) },
                            leadingIcon = { Icon(Icons.Rounded.PhotoCamera, null, tint = Color.White) },
                            onClick = {
                                expanded = false
                                val opened = openAndroidIntent(context, Intent(MediaStore.ACTION_IMAGE_CAPTURE))
                                queueAction(
                                    "Foto",
                                    if (opened) "Fotocamera Android aperta. Scatta la foto e allegala al task quando pronta." else "Nessuna app fotocamera disponibile. Seleziona una foto esistente dal menu file.",
                                    "Acquisisci una foto e usala come allegato per la conversazione."
                                )
                            }
                        )
                        HorizontalDivider(color = AppColors.Border)
                        DropdownMenuItem(
                            text = { Text("Passa a modalita Chat", color = Color.White) },
                            leadingIcon = { Icon(Icons.Rounded.ChatBubbleOutline, null, tint = Color.White) },
                            onClick = {
                                expanded = false
                                onModeChange("Chat")
                                onAction("Modalita", "Chat attiva: messaggi normali, nessun task agente automatico.", "")
                            }
                        )
                        DropdownMenuItem(
                            text = { Text("Passa a modalita Agente", color = Color.White) },
                            leadingIcon = { Icon(Icons.Rounded.SmartToy, null, tint = Color.White) },
                            onClick = {
                                expanded = false
                                onModeChange("Agente")
                                onAction("Modalita", "Agente attivo: usa Hermes Runs/Jobs se disponibili, altrimenti fallback locale.", "")
                            }
                        )
                        HorizontalDivider(color = AppColors.Border)
                        DropdownMenuItem(
                            text = { Text("Crea immagine", color = Color.White) },
                            leadingIcon = { Icon(Icons.Rounded.Image, null, tint = Color.White) },
                            onClick = {
                                expanded = false
                                queueAction(
                                    "Immagine",
                                    "Generazione immagine richiedera' tool Hermes dedicato e conferma prima di chiamate esterne.",
                                    "Prepara una richiesta di generazione immagine, ma chiedi conferma prima di usare tool esterni."
                                )
                            }
                        )
                        DropdownMenuItem(
                            text = { Text("Deep Research locale", color = Color.White) },
                            leadingIcon = { Icon(Icons.AutoMirrored.Rounded.ManageSearch, null, tint = Color.White) },
                            onClick = {
                                expanded = false
                                queueAction(
                                    "Deep Research",
                                    "Ricerca approfondita locale; rete solo dopo approvazione esplicita.",
                                    "Esegui una ricerca approfondita e cita fonti, usando rete solo dopo approvazione."
                                )
                            }
                        )
                        DropdownMenuItem(
                            text = { Text("Ricerca web autorizzata", color = Color.White) },
                            leadingIcon = { Icon(Icons.Rounded.Language, null, tint = Color.White) },
                            onClick = {
                                expanded = false
                                queueAction(
                                    "Web",
                                    "Ricerca web marcata come azione autorizzabile: nessuna rete fuori LAN/VPN senza conferma.",
                                    "Cerca sul web informazioni aggiornate, chiedendo conferma prima di uscire dalla LAN/VPN."
                                )
                            }
                        )
                        DropdownMenuItem(
                            text = { Text("Progetti e workspace", color = Color.White) },
                            leadingIcon = { Icon(Icons.Rounded.FolderOpen, null, tint = Color.White) },
                            onClick = {
                                expanded = false
                                queueAction(
                                    "Workspace",
                                    "Workspace/progetti saranno collegati ai Jobs Hermes con audit trail.",
                                    "Lavora sul workspace o progetto selezionato e mostra piano prima di modificare file."
                                )
                            }
                        )
                    }
                }
                Spacer(modifier = Modifier.weight(1f))
                IconButton(onClick = {
                    val opened = openAndroidIntent(
                        context,
                        Intent(RecognizerIntent.ACTION_RECOGNIZE_SPEECH).apply {
                            putExtra(RecognizerIntent.EXTRA_LANGUAGE_MODEL, RecognizerIntent.LANGUAGE_MODEL_FREE_FORM)
                            putExtra(RecognizerIntent.EXTRA_PROMPT, "Detta il prompt per Hermes")
                        }
                    )
                    onAction(
                        "Voce",
                        if (opened) "Dettatura Android aperta. Inserisci o rifinisci il testo nel prompt." else "Dettatura non disponibile su questo dispositivo. Scrivi la nota vocale nel prompt.",
                        "Trascrivi questa nota vocale e usala come contesto: "
                    )
                }) {
                    Icon(Icons.Rounded.Mic, contentDescription = "Dettatura", tint = AppColors.Muted)
                }
                Button(
                    modifier = Modifier.size(44.dp),
                    onClick = onSend,
                    enabled = value.isNotBlank() && !isBusy,
                    colors = ButtonDefaults.buttonColors(containerColor = Color.White, contentColor = Color.Black),
                    shape = CircleShape,
                    contentPadding = PaddingValues(0.dp)
                ) {
                    Icon(Icons.Rounded.ArrowUpward, contentDescription = "Invia")
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
    var pendingDelete by remember { mutableStateOf<ArchiveItem?>(null) }
    val savedConversations = remember(refreshKey) { loadConversations(context) }
    val savedProjects = savedConversations.count { it.kind == "Progetto" }
    val savedChats = savedConversations.count { it.kind == "Chat" || it.kind == "Task" }
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
            Text("Ricerca locale persistente per chat, progetti e task recenti.", color = AppColors.Muted)
        }
        item {
            Card(colors = CardDefaults.cardColors(containerColor = AppColors.Surface), shape = RoundedCornerShape(20.dp)) {
                Column(modifier = Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                    Text("Stato archivio", color = Color.White, fontWeight = FontWeight.SemiBold)
                    Text("Conversazioni: $savedChats | Progetti: $savedProjects | Totale salvati: ${savedConversations.size}", color = AppColors.Muted)
                    FlowRow(
                        horizontalArrangement = Arrangement.spacedBy(8.dp),
                        verticalArrangement = Arrangement.spacedBy(8.dp)
                    ) {
                        Button(onClick = {
                            copyArchiveToClipboard(context)
                            status = "Archivio copiato negli appunti."
                        }) {
                            Text("Export")
                        }
                        Button(onClick = {
                            filter = "Progetto"
                            status = "Filtro: Progetto"
                        }) {
                            Text("Progetti")
                        }
                        Button(onClick = {
                            filter = "Chat"
                            status = "Filtro: Chat"
                        }) {
                            Text("Recenti")
                        }
                    }
                }
            }
        }
        item {
            Card(colors = CardDefaults.cardColors(containerColor = AppColors.Surface), shape = RoundedCornerShape(20.dp)) {
                Column(modifier = Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
                    SettingsField("Cerca", query, { query = it })
                    FlowRow(
                        horizontalArrangement = Arrangement.spacedBy(8.dp),
                        verticalArrangement = Arrangement.spacedBy(8.dp)
                    ) {
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
                },
                onRename = { newTitle ->
                    if (item.id == null) {
                        status = "Apri o salva prima di rinominare."
                    } else if (renameConversation(context, item.id, newTitle)) {
                        status = "Rinominato: $newTitle"
                        refreshKey++
                    } else {
                        status = "Elemento non trovato."
                    }
                },
                onDelete = {
                    if (item.id == null) {
                        status = "Template non eliminabile."
                    } else {
                        pendingDelete = item
                    }
                }
            )
        }
    }

    pendingDelete?.let { item ->
        AlertDialog(
            onDismissRequest = { pendingDelete = null },
            containerColor = AppColors.Surface,
            title = {
                Text("Conferma eliminazione", color = Color.White, fontWeight = FontWeight.SemiBold)
            },
            text = {
                Text(
                    "Vuoi eliminare davvero \"${item.title}\" dall'archivio locale?",
                    color = AppColors.Muted
                )
            },
            confirmButton = {
                Button(
                    onClick = {
                        pendingDelete = null
                        if (item.id != null && deleteConversation(context, item.id)) {
                            status = "Eliminato: ${item.title}"
                            refreshKey++
                        } else {
                            status = "Elemento non trovato."
                        }
                    },
                    colors = ButtonDefaults.buttonColors(
                        containerColor = Color(0xFF8E2E3F),
                        contentColor = Color.White
                    )
                ) {
                    Text("Elimina")
                }
            },
            dismissButton = {
                Button(
                    onClick = { pendingDelete = null },
                    colors = ButtonDefaults.buttonColors(
                        containerColor = AppColors.AssistantBubble,
                        contentColor = Color.White
                    )
                ) {
                    Text("Annulla")
                }
            }
        )
    }
}

@OptIn(ExperimentalLayoutApi::class)
@Composable
private fun ArchiveCard(
    item: ArchiveItem,
    onOpen: () -> Unit,
    onPin: () -> Unit,
    onRename: (String) -> Unit,
    onDelete: () -> Unit
) {
    var renameText by remember(item.id, item.title) { mutableStateOf(item.title) }

    Card(colors = CardDefaults.cardColors(containerColor = AppColors.AssistantBubble), shape = RoundedCornerShape(20.dp)) {
        Column(modifier = Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Text(item.title, color = Color.White, fontWeight = FontWeight.SemiBold, modifier = Modifier.weight(1f))
                Text(item.kind, color = AppColors.Accent, fontSize = 12.sp)
                if (item.id != null) {
                    IconButton(onClick = onDelete, modifier = Modifier.size(36.dp)) {
                        Icon(
                            Icons.Rounded.Delete,
                            contentDescription = "Elimina elemento archivio",
                            tint = Color(0xFFFF7B8E)
                        )
                    }
                }
            }
            Text(item.description, color = AppColors.Muted)
            Text(item.prompt, color = Color.White, fontSize = 13.sp)
            if (item.id != null) {
                SettingsField("Rinomina", renameText, { renameText = it })
            }
            FlowRow(
                horizontalArrangement = Arrangement.spacedBy(8.dp),
                verticalArrangement = Arrangement.spacedBy(8.dp)
            ) {
                Button(onClick = onOpen) { Text("Apri") }
                Button(onClick = onPin) { Text("Segna") }
                if (item.id != null) {
                    Button(onClick = { onRename(renameText.trim()) }) { Text("Rinomina") }
                    Button(onClick = onDelete) { Text("Elimina") }
                }
            }
        }
    }
}

@Composable
private fun TasksScreen(context: Context, settings: AppSettings) {
    val scope = rememberCoroutineScope()
    val tasks = remember {
        mutableStateListOf<AgentTask>().apply {
            addAll(loadTasks(context))
        }
    }
    var title by remember { mutableStateOf("") }
    var detail by remember { mutableStateOf("") }
    var requiresApproval by remember { mutableStateOf(true) }
    var status by remember { mutableStateOf("Pronto.") }

    LazyColumn(
        modifier = Modifier
            .fillMaxSize()
            .padding(24.dp),
        verticalArrangement = Arrangement.spacedBy(14.dp)
    ) {
        item {
            Text("Jobs", color = Color.White, fontSize = 30.sp, fontWeight = FontWeight.SemiBold)
            Spacer(modifier = Modifier.height(8.dp))
            Text("Coda Jobs persistente. Usa Hermes Jobs API quando disponibile, altrimenti fallback locale.", color = AppColors.Muted)
        }
        item {
            Card(colors = CardDefaults.cardColors(containerColor = AppColors.Surface), shape = RoundedCornerShape(20.dp)) {
                Column(modifier = Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
                    Text("Nuovo job", color = Color.White, fontWeight = FontWeight.SemiBold)
                    SettingsField("Titolo", title, { title = it })
                    SettingsField("Istruzioni", detail, { detail = it })
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Text("Richiedi conferma", color = Color.White, modifier = Modifier.weight(1f))
                        Switch(checked = requiresApproval, onCheckedChange = { requiresApproval = it })
                    }
                    FlowRow(
                        horizontalArrangement = Arrangement.spacedBy(10.dp),
                        verticalArrangement = Arrangement.spacedBy(10.dp)
                    ) {
                        Button(onClick = {
                            if (title.isBlank()) {
                                status = "Titolo obbligatorio."
                                return@Button
                            }
                            val localTask = AgentTask(
                                id = "task_${System.currentTimeMillis()}",
                                title = title.trim(),
                                mode = if (settings.demoMode) "Locale" else "Job",
                                status = if (requiresApproval) "In attesa approvazione" else "Pronto",
                                detail = detail.ifBlank { "Esegui con piano prima di azioni rischiose." }.trim(),
                                requiresApproval = requiresApproval
                            )
                            tasks.add(0, localTask)
                            saveTasks(context, tasks)
                            title = ""
                            detail = ""
                            requiresApproval = true
                            status = "Invio job a Hermes..."

                            scope.launch {
                                val result = queueTaskRequest(settings, localTask, loadGatewaySecret(context))
                                replaceTask(tasks, result.task)
                                saveTasks(context, tasks)
                                status = result.message
                            }
                        }) {
                            Text("Accoda")
                        }
                        Button(onClick = {
                            title = "Analizza workspace"
                            detail = "Ispeziona il progetto con Hermes Agent, individua rischi e proponi un piano operativo."
                            requiresApproval = true
                            status = "Template workspace caricato."
                        }) {
                            Text("Template")
                        }
                        Button(onClick = {
                            title = "Controlla home-server Hermes"
                            detail = "Verifica API Hermes, modello, health e capabilities."
                            requiresApproval = true
                            status = "Template server caricato."
                        }) {
                            Text("Server")
                        }
                    }
                    Text(status, color = AppColors.Muted)
                }
            }
        }
        if (tasks.isEmpty()) {
            item {
                Text("Nessun job ancora.", color = AppColors.Muted)
            }
        }
        items(tasks) { task ->
            TaskCard(
                task = task,
                onApprove = {
                    scope.launch {
                        val result = updateTaskRequest(settings, task, "run", loadGatewaySecret(context))
                        replaceTask(tasks, result.task)
                        saveTasks(context, tasks)
                        status = result.message
                    }
                },
                onDeny = {
                    scope.launch {
                        val result = updateTaskRequest(settings, task, "pause", loadGatewaySecret(context))
                        replaceTask(tasks, result.task)
                        saveTasks(context, tasks)
                        status = result.message
                    }
                },
                onDone = {
                    scope.launch {
                        val result = updateTaskRequest(settings, task, "delete", loadGatewaySecret(context))
                        replaceTask(tasks, result.task)
                        saveTasks(context, tasks)
                        status = result.message
                    }
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
            Text("Modalita: ${task.mode} | Origine: ${task.source} | Conferma: ${if (task.requiresApproval) "si" else "no"}", color = AppColors.Muted, fontSize = 12.sp)
            Text(task.detail, color = Color.White)
            FlowRow(
                horizontalArrangement = Arrangement.spacedBy(8.dp),
                verticalArrangement = Arrangement.spacedBy(8.dp)
            ) {
                Button(onClick = onApprove) { Text("Run") }
                Button(onClick = onDeny) { Text("Pausa") }
                Button(onClick = onDone) { Text("Elimina") }
            }
        }
    }
}

@Composable
private fun ServerScreen(context: Context, settings: AppSettings) {
    val scope = rememberCoroutineScope()
    var wsProbe by remember(settings) {
        mutableStateOf(
            GatewayWsProbe(
                wsUrl = "${settings.gatewayUrl.trimEnd('/')}/capabilities",
                connected = false,
                status = "Nessun controllo capabilities eseguito.",
                detail = "Leggi /v1/capabilities e /v1/models per verificare Hermes."
            )
        )
    }
    var snapshot by remember(settings) {
        mutableStateOf(
            ServerSnapshot(
                gateway = settings.gatewayUrl,
                model = settings.model,
                providerDetail = "Provider: ${settings.provider} | API: ${settings.preferredApi}",
                inferenceEndpoint = settings.inferenceEndpoint,
                policy = settings.accessMode,
                statusMessage = if (settings.demoMode) "Fallback locale attivo. Provero' comunque a usare Hermes." else "Solo Hermes. Verifica lo stato del server."
            )
        )
    }

    LaunchedEffect(settings) {
        snapshot = loadServerSnapshot(settings, loadGatewaySecret(context))
    }

    LazyColumn(
        modifier = Modifier
            .fillMaxSize()
            .padding(24.dp),
        verticalArrangement = Arrangement.spacedBy(14.dp)
    ) {
        item {
            Text("Hermes Server", color = Color.White, fontSize = 30.sp, fontWeight = FontWeight.SemiBold)
            Spacer(modifier = Modifier.height(8.dp))
            Text("Dashboard Hermes Agent API: health, detailed health, models e capabilities.", color = AppColors.Muted)
        }
        item {
            ServerMetric("Hermes API", snapshot.gateway, "Health endpoint: ${hermesRoot(settings)}/health")
        }
        item {
            ServerMetric("Capabilities", wsProbe.wsUrl, wsProbe.status)
        }
        item {
            ServerMetric("Modello", snapshot.model, snapshot.providerDetail)
        }
        item {
            ServerMetric("API lato server", snapshot.inferenceEndpoint, "Il client parla a Hermes API, non direttamente al runtime modello.")
        }
        item {
            ServerMetric("Sicurezza", snapshot.policy, "API key via Authorization Bearer; esposizione consigliata solo LAN/Tailscale.")
        }
        item {
            Card(colors = CardDefaults.cardColors(containerColor = AppColors.Surface), shape = RoundedCornerShape(20.dp)) {
                Column(modifier = Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
                    Text("Azioni", color = Color.White, fontWeight = FontWeight.SemiBold)
                    Text(snapshot.statusMessage, color = AppColors.Muted)
                    FlowRow(
                        horizontalArrangement = Arrangement.spacedBy(10.dp),
                        verticalArrangement = Arrangement.spacedBy(10.dp)
                    ) {
                        Button(onClick = {
                            val error = validateHttpUrl(settings.gatewayUrl, "Hermes API URL")
                            if (error != null) {
                                snapshot = snapshot.copy(statusMessage = error)
                                return@Button
                            }
                            val healthUrl = "${hermesRoot(settings)}/health"
                            snapshot = snapshot.copy(statusMessage = "Test: $healthUrl")
                            scope.launch {
                                snapshot = snapshot.copy(statusMessage = testGateway(healthUrl, loadGatewaySecret(context)))
                            }
                        }) {
                            Text("Test Hermes")
                        }
                        Button(onClick = {
                            scope.launch {
                                snapshot = loadServerSnapshot(settings, loadGatewaySecret(context))
                            }
                        }) {
                            Text("Aggiorna stato")
                        }
                        Button(onClick = {
                            snapshot = snapshot.copy(statusMessage = "Contratto Hermes: GET /health, GET /health/detailed, GET /v1/models, GET /v1/capabilities, POST /v1/responses, POST /v1/chat/completions, POST /v1/runs, GET/POST /api/jobs.")
                        }) {
                            Text("Mostra API")
                        }
                    }
                }
            }
        }
        item {
            Card(colors = CardDefaults.cardColors(containerColor = AppColors.Surface), shape = RoundedCornerShape(20.dp)) {
                Column(modifier = Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
                    Text("Capabilities Hermes", color = Color.White, fontWeight = FontWeight.SemiBold)
                    Text(wsProbe.detail, color = AppColors.Muted)
                    Button(onClick = {
                        wsProbe = wsProbe.copy(status = "Lettura capabilities...", detail = "Chiamata a /v1/capabilities e /v1/models.")
                        scope.launch {
                            val capabilities = runCatching { httpGet("${settings.gatewayUrl.trimEnd('/')}/capabilities", loadGatewaySecret(context)) }.getOrElse { it.message ?: it.javaClass.simpleName }
                            val models = runCatching { httpGet("${settings.gatewayUrl.trimEnd('/')}/models", loadGatewaySecret(context)) }.getOrElse { it.message ?: it.javaClass.simpleName }
                            wsProbe = GatewayWsProbe(
                                wsUrl = "${settings.gatewayUrl.trimEnd('/')}/capabilities",
                                connected = true,
                                status = "Capabilities lette.",
                                detail = "Capabilities e models letti da Hermes.",
                                capabilityLines = listOf("Capabilities: ${capabilities.limitText(240)}", "Models: ${models.limitText(240)}")
                            )
                        }
                    }) {
                        Text("Leggi capabilities")
                    }
                    if (wsProbe.capabilityLines.isNotEmpty()) {
                        Column(verticalArrangement = Arrangement.spacedBy(6.dp)) {
                            wsProbe.capabilityLines.forEach { line ->
                                Text(line, color = AppColors.Muted, fontSize = 12.sp)
                            }
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
private fun OperatorScreen(context: Context, settings: AppSettings) {
    val scope = rememberCoroutineScope()
    var method by remember { mutableStateOf("GET /health") }
    var params by remember { mutableStateOf("") }
    var approvalId by remember { mutableStateOf("") }
    var baseHash by remember { mutableStateOf("") }
    var configPatch by remember { mutableStateOf("{\"ops\":[]}") }
    var workspacePath by remember { mutableStateOf("") }
    var workspaceText by remember { mutableStateOf("") }
    var adminPath by remember { mutableStateOf("") }
    var status by remember { mutableStateOf("Pronto.") }
    var summary by remember { mutableStateOf("Nessuna risposta.") }
    var raw by remember { mutableStateOf("") }

    LazyColumn(
        modifier = Modifier
            .fillMaxSize()
            .padding(24.dp),
        verticalArrangement = Arrangement.spacedBy(14.dp)
    ) {
        item {
            Text("Runs", color = Color.White, fontSize = 30.sp, fontWeight = FontWeight.SemiBold)
            Spacer(modifier = Modifier.height(8.dp))
            Text("Operazioni reali verso Hermes Agent API: health, models, capabilities, runs e jobs.", color = AppColors.Muted)
        }
        item {
            Card(colors = CardDefaults.cardColors(containerColor = AppColors.Surface), shape = RoundedCornerShape(20.dp)) {
                Column(modifier = Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
                    Text("Preset Hermes", color = Color.White, fontWeight = FontWeight.SemiBold)
                    OPERATOR_PRESETS.groupBy { it.group }.forEach { (group, presets) ->
                        Text(group, color = AppColors.Muted, fontSize = 12.sp)
                        presets.forEach { preset ->
                            Button(
                                modifier = Modifier.fillMaxWidth(),
                                onClick = {
                                    method = preset.method
                                    params = preset.params
                                    status = "${preset.method}..."
                                    summary = "Attesa risposta Hermes..."
                                    raw = ""
                                    scope.launch {
                                        val result = hermesHttpCall(settings, loadGatewaySecret(context), preset.method, preset.params)
                                        status = result.status
                                        summary = result.summary
                                        raw = result.rawJson.ifBlank { result.summary }
                                    }
                                }
                            ) {
                                Text(preset.label)
                            }
                        }
                    }
                }
            }
        }
        item {
            Card(colors = CardDefaults.cardColors(containerColor = AppColors.Surface), shape = RoundedCornerShape(20.dp)) {
                Column(modifier = Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
                    Text("Jobs", color = Color.White, fontWeight = FontWeight.SemiBold)
                    SettingsField("Job ID", approvalId, { approvalId = it })
                    OperatorActionButton("Lista") { runOperatorRpc(context, settings, "GET /api/jobs", "", { status = it }, { summary = it }, { raw = it }) }
                    OperatorActionButton("Run") { runOperatorRpc(context, settings, "POST /api/jobs/${approvalId.jsonEscaped()}/run", "{}", { status = it }, { summary = it }, { raw = it }) }
                    OperatorActionButton("Pausa") { runOperatorRpc(context, settings, "POST /api/jobs/${approvalId.jsonEscaped()}/pause", "{}", { status = it }, { summary = it }, { raw = it }) }
                    OperatorActionButton("Elimina") { runOperatorRpc(context, settings, "DELETE /api/jobs/${approvalId.jsonEscaped()}", "", { status = it }, { summary = it }, { raw = it }) }
                }
            }
        }
        item {
            Card(colors = CardDefaults.cardColors(containerColor = AppColors.Surface), shape = RoundedCornerShape(20.dp)) {
                Column(modifier = Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
                    Text("Runs manuali", color = Color.White, fontWeight = FontWeight.SemiBold)
                    SettingsField("Run ID", baseHash, { baseHash = it })
                    SettingsField("Input run", configPatch, { configPatch = it })
                    OperatorActionButton("Capabilities") { runOperatorRpc(context, settings, "GET /v1/capabilities", "", { status = it }, { summary = it }, { raw = it }) }
                    OperatorActionButton("Crea run") { runOperatorRpc(context, settings, "POST /v1/runs", "{\"model\":\"hermes-agent\",\"input\":\"${configPatch.jsonEscaped()}\"}", { status = it }, { summary = it }, { raw = it }) }
                    OperatorActionButton("Models") { runOperatorRpc(context, settings, "GET /v1/models", "", { status = it }, { summary = it }, { raw = it }) }
                }
            }
        }
        item {
            Card(colors = CardDefaults.cardColors(containerColor = AppColors.Surface), shape = RoundedCornerShape(20.dp)) {
                Column(modifier = Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
                    Text("Diagnostica", color = Color.White, fontWeight = FontWeight.SemiBold)
                    SettingsField("Filtro job", workspacePath, { workspacePath = it })
                    SettingsField("Input run rapido", workspaceText, { workspaceText = it })
                    OperatorActionButton("Jobs") { runOperatorRpc(context, settings, "GET /api/jobs", "", { status = it }, { summary = it }, { raw = it }) }
                    OperatorActionButton("Health") { runOperatorRpc(context, settings, "GET /health/detailed", "", { status = it }, { summary = it }, { raw = it }) }
                    OperatorActionButton("Run") { runOperatorRpc(context, settings, "POST /v1/runs", "{\"model\":\"hermes-agent\",\"input\":\"${workspaceText.jsonEscaped()}\"}", { status = it }, { summary = it }, { raw = it }) }
                }
            }
        }
        if (false) {
        item {
            Card(colors = CardDefaults.cardColors(containerColor = AppColors.Surface), shape = RoundedCornerShape(20.dp)) {
                Column(modifier = Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
                    Text("Admin Bridge", color = Color.White, fontWeight = FontWeight.SemiBold)
                    SettingsField("Path/log", adminPath, { adminPath = it })
                    OperatorActionButton("Status") { runAdminBridge(context, settings, "GET", "/v1/status", null, { status = it }, { summary = it }, { raw = it }) }
                    OperatorActionButton("Doctor") { runAdminBridge(context, settings, "POST", "/v1/actions/doctor", null, { status = it }, { summary = it }, { raw = it }) }
                    OperatorActionButton("Audit") { runAdminBridge(context, settings, "POST", "/v1/actions/security-audit", null, { status = it }, { summary = it }, { raw = it }) }
                    OperatorActionButton("Restart") { runAdminBridge(context, settings, "POST", "/v1/actions/restart-gateway", null, { status = it }, { summary = it }, { raw = it }) }
                    OperatorActionButton("Tail log") { runAdminBridge(context, settings, "POST", "/v1/logs/tail", JSONObject().put("path", adminPath).put("lines", 200), { status = it }, { summary = it }, { raw = it }) }
                }
            }
        }
        }
        item {
            Card(colors = CardDefaults.cardColors(containerColor = AppColors.Surface), shape = RoundedCornerShape(20.dp)) {
                Column(modifier = Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
                    Text("Endpoint manuale", color = Color.White, fontWeight = FontWeight.SemiBold)
                    SettingsField("Metodo + path", method, { method = it })
                    SettingsField("Body JSON", params, { params = it })
                    Button(onClick = {
                        status = "${method.trim()}..."
                        summary = "Attesa risposta Hermes..."
                        raw = ""
                        scope.launch {
                            val result = hermesHttpCall(settings, loadGatewaySecret(context), method, params)
                            status = result.status
                            summary = result.summary
                            raw = result.rawJson.ifBlank { result.summary }
                        }
                    }) {
                        Text("Esegui")
                    }
                    Text(status, color = AppColors.Muted)
                    Text(summary, color = AppColors.Muted, fontSize = 12.sp)
                }
            }
        }
        item {
            Card(colors = CardDefaults.cardColors(containerColor = AppColors.Surface), shape = RoundedCornerShape(20.dp)) {
                Text(
                    modifier = Modifier.padding(16.dp),
                    text = raw.ifBlank { "Nessuna risposta." },
                    color = Color.White,
                    fontSize = 12.sp
                )
            }
        }
    }
}

@Composable
private fun OperatorActionButton(label: String, onClick: () -> Unit) {
    Button(
        modifier = Modifier.fillMaxWidth(),
        onClick = onClick
    ) {
        Text(label)
    }
}

private fun runOperatorRpc(
    context: Context,
    settings: AppSettings,
    method: String,
    params: String,
    setStatus: (String) -> Unit,
    setSummary: (String) -> Unit,
    setRaw: (String) -> Unit
) {
    setStatus("$method...")
    setSummary("Attesa risposta Hermes...")
    setRaw("")
    kotlinx.coroutines.CoroutineScope(Dispatchers.Main).launch {
        val result = hermesHttpCall(settings, loadGatewaySecret(context), method, params)
        setStatus(result.status)
        setSummary(result.summary)
        setRaw(result.rawJson.ifBlank { result.summary })
    }
}

private fun runAdminBridge(
    context: Context,
    settings: AppSettings,
    method: String,
    path: String,
    payload: JSONObject?,
    setStatus: (String) -> Unit,
    setSummary: (String) -> Unit,
    setRaw: (String) -> Unit
) {
    setStatus("Admin Bridge $path...")
    setSummary("Attesa risposta Admin Bridge...")
    setRaw("")
    kotlinx.coroutines.CoroutineScope(Dispatchers.Main).launch {
        val result = adminBridgeCall(settings, loadGatewaySecret(context), method, path, payload)
        setStatus(result.status)
        setSummary(result.summary)
        setRaw(result.rawJson.ifBlank { result.summary })
    }
}

@Composable
private fun ProfileScreen(context: Context, settings: AppSettings) {
    val scope = rememberCoroutineScope()
    val conversations = remember { loadConversations(context) }
    val version = remember { appVersion(context) }
    var updateState by remember { mutableStateOf(UpdateDownloadState()) }

    LazyColumn(
        modifier = Modifier
            .fillMaxSize()
            .padding(24.dp),
        verticalArrangement = Arrangement.spacedBy(14.dp)
    ) {
        item {
            Text("Profilo", color = Color.White, fontSize = 30.sp, fontWeight = FontWeight.SemiBold)
            Spacer(modifier = Modifier.height(8.dp))
            Text("Profilo locale. Nessun account cloud, nessun token provider nel client.", color = AppColors.Muted)
        }
        item {
            Card(colors = CardDefaults.cardColors(containerColor = AppColors.Surface), shape = RoundedCornerShape(24.dp)) {
                Row(
                    modifier = Modifier.padding(18.dp),
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Image(
                        painter = painterResource(id = R.drawable.chatclaw_logo),
                        contentDescription = "Logo Hermes Hub",
                        contentScale = ContentScale.Crop,
                        modifier = Modifier
                            .size(64.dp)
                            .clip(RoundedCornerShape(18.dp))
                    )
                    Column(modifier = Modifier.padding(start = 14.dp), verticalArrangement = Arrangement.spacedBy(4.dp)) {
                        Text("Matteo", color = Color.White, fontSize = 20.sp, fontWeight = FontWeight.SemiBold)
                        Text("Home-server Hermes Agent locale", color = AppColors.Muted)
                        Text("App: $version", color = AppColors.Muted, fontSize = 12.sp)
                    }
                }
            }
        }
        item {
            ServerMetric("Hermes API", settings.gatewayUrl, if (settings.demoMode) "Fallback locale attivo" else "Solo Hermes")
        }
        item {
            ServerMetric("Archivio locale", "${conversations.size} elementi", "Cronologia e progetti salvati sul dispositivo.")
        }
        item {
            ServerMetric("Privacy", "Locale-first", "Chat/settings restano sul dispositivo finche' non colleghi Hermes. API key cifrata localmente.")
        }
        item {
            ServerMetric("Parita Windows", "Allineata", "Chat, archivio, progetti/recenti, jobs, Hermes server, runs, settings e profilo presenti anche su Android.")
        }
        item {
            Card(colors = CardDefaults.cardColors(containerColor = AppColors.Surface), shape = RoundedCornerShape(20.dp)) {
                Column(modifier = Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
                    Text("Aggiornamenti", color = Color.White, fontWeight = FontWeight.SemiBold)
                    Text(updateState.status, color = AppColors.Muted)
                    if (updateState.progress != null) {
                        LinearProgressIndicator(
                            progress = { updateState.progress ?: 0f },
                            modifier = Modifier.fillMaxWidth(),
                            color = AppColors.Accent,
                            trackColor = Color(0xFF424242)
                        )
                        Text(
                            downloadProgressLabel(updateState.progress, updateState.downloadLabel),
                            color = AppColors.Muted,
                            fontSize = 12.sp
                        )
                    }
                    FlowRow(
                        horizontalArrangement = Arrangement.spacedBy(8.dp),
                        verticalArrangement = Arrangement.spacedBy(8.dp)
                    ) {
                        Button(onClick = {
                            updateState = updateState.copy(
                                status = "Controllo GitHub Releases...",
                                progress = null,
                                downloadLabel = "",
                                downloadedApkPath = null,
                                isDownloading = false
                            )
                            scope.launch {
                                val result = checkGithubUpdate(version)
                                val downloadedApk = if (result.hasUpdate) {
                                    result.latestVersion?.let { findDownloadedUpdateApk(context, it) }
                                } else {
                                    null
                                }
                                updateState = updateState.copy(
                                    status = if (downloadedApk != null) {
                                        "Aggiornamento gia' scaricato. Premi Aggiorna per installarlo."
                                    } else if (result.hasUpdate && result.assetUrl != null) {
                                        "${result.message} Scarica l'APK dentro l'app e poi premi Aggiorna."
                                    } else {
                                        result.message
                                    },
                                    releaseAssetUrl = result.assetUrl,
                                    latestVersion = result.latestVersion,
                                    hasUpdate = result.hasUpdate,
                                    progress = if (downloadedApk != null) 1f else null,
                                    downloadLabel = downloadedApk?.length()?.toReadableFileSize() ?: "",
                                    downloadedApkPath = downloadedApk?.absolutePath
                                )
                            }
                        }) {
                            Text("Controlla")
                        }
                        if (updateState.hasUpdate && updateState.releaseAssetUrl != null && updateState.downloadedApkPath == null && !updateState.isDownloading) {
                            Button(onClick = {
                                val assetUrl = updateState.releaseAssetUrl ?: return@Button
                                scope.launch {
                                    updateState = updateState.copy(
                                        status = "Scaricamento APK in corso...",
                                        isDownloading = true,
                                        progress = 0f,
                                        downloadLabel = ""
                                    )
                                    val downloaded = downloadUpdateApk(
                                        context = context,
                                        assetUrl = assetUrl,
                                        version = updateState.latestVersion ?: version
                                    ) { fraction, status, label ->
                                        updateState = updateState.copy(
                                            progress = fraction,
                                            status = status,
                                            downloadLabel = label,
                                            isDownloading = true
                                        )
                                    }

                                    updateState = if (downloaded != null) {
                                        updateState.copy(
                                            status = "APK pronto. Premi Aggiorna per avviare l'installazione Android.",
                                            downloadedApkPath = downloaded.absolutePath,
                                            isDownloading = false,
                                            progress = 1f,
                                            downloadLabel = downloaded.length().toReadableFileSize()
                                        )
                                    } else {
                                        updateState.copy(
                                            status = "Download non riuscito. Premi Controlla e riprova.",
                                            downloadedApkPath = null,
                                            isDownloading = false,
                                            progress = null,
                                            downloadLabel = ""
                                        )
                                    }
                                }
                            }) {
                                Text("Scarica")
                            }
                        }
                        if (updateState.downloadedApkPath != null && !updateState.isDownloading) {
                            Button(onClick = {
                                val apkPath = updateState.downloadedApkPath ?: return@Button
                                val status = installDownloadedApk(context, apkPath)
                                updateState = updateState.copy(status = status)
                            }) {
                                Text("Aggiorna")
                            }
                        }
                    }
                }
            }
        }
    }
}

@Composable
private fun SettingsScreen(
    settings: AppSettings,
    onSave: (AppSettings) -> Unit,
    onReset: () -> Unit
) {
    val context = LocalContext.current
    val scope = rememberCoroutineScope()
    var gatewayUrl by remember(settings) { mutableStateOf(settings.gatewayUrl) }
    var gatewayWsUrl by remember(settings) { mutableStateOf(settings.gatewayWsUrl) }
    var adminBridgeUrl by remember(settings) { mutableStateOf(settings.adminBridgeUrl) }
    var gatewaySecret by remember(settings) { mutableStateOf("") }
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
        Text("Impostazioni salvate sul dispositivo. Hermes API key cifrata con Android Keystore.", color = AppColors.Muted)
        Spacer(modifier = Modifier.height(18.dp))
        LazyColumn(verticalArrangement = Arrangement.spacedBy(10.dp)) {
            item { SettingsField("Hermes API URL", gatewayUrl, { gatewayUrl = it }) }
            item {
                SettingsPasswordField(
                    label = if (hasGatewaySecret(context)) "Hermes API key (gia' salvata)" else "Hermes API key",
                    value = gatewaySecret,
                    onValueChange = { gatewaySecret = it }
                )
            }
            item { SettingsField("Provider", provider, { provider = it }) }
            item { SettingsField("Endpoint API lato server", inferenceEndpoint, { inferenceEndpoint = it }) }
            item { SettingsField("API preferita", preferredApi, { preferredApi = it }) }
            item { SettingsField("Modello", model, { model = it }) }
            item { SettingsField("Accesso", accessMode, { accessMode = it }) }
            item {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Text("Fallback locale", color = Color.White, modifier = Modifier.weight(1f))
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
                Column(verticalArrangement = Arrangement.spacedBy(10.dp)) {
                    FlowRow(
                        horizontalArrangement = Arrangement.spacedBy(10.dp),
                        verticalArrangement = Arrangement.spacedBy(10.dp)
                    ) {
                        Button(onClick = {
                            val candidate = AppSettings(
                                gatewayUrl = gatewayUrl.trim(),
                                gatewayWsUrl = "",
                                adminBridgeUrl = hermesRoot(AppSettings(gatewayUrl = gatewayUrl.trim())),
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
                                if (gatewaySecret.isNotBlank()) {
                                    saveGatewaySecret(context, gatewaySecret)
                                    gatewaySecret = ""
                                }
                                status = if (hasGatewaySecret(context)) {
                                    "Impostazioni salvate. Hermes API key cifrata in Android Keystore."
                                } else {
                                    "Impostazioni salvate. Nessuna Hermes API key salvata."
                                }
                            } else {
                                status = error
                            }
                        }) {
                            Text("Salva")
                        }
                        Button(onClick = {
                            val candidate = AppSettings(
                                gatewayUrl = gatewayUrl.trim(),
                                gatewayWsUrl = "",
                                adminBridgeUrl = hermesRoot(AppSettings(gatewayUrl = gatewayUrl.trim())),
                                provider = provider.trim(),
                                inferenceEndpoint = inferenceEndpoint.trim(),
                                preferredApi = preferredApi.trim(),
                                model = model.trim(),
                                accessMode = accessMode.trim(),
                                demoMode = demoMode
                            )
                            val error = validateHttpUrl(candidate.gatewayUrl, "Hermes API URL")
                            if (error != null) {
                                status = error
                                return@Button
                            }
                        status = "Leggo capabilities Hermes..."
                        scope.launch {
                            status = runCatching { httpGet("${candidate.gatewayUrl.trimEnd('/')}/capabilities", gatewaySecret.ifBlank { loadGatewaySecret(context) }) }
                                .getOrElse { "Capabilities non leggibili: ${it.message ?: it.javaClass.simpleName}" }
                        }
                        }) {
                            Text("Capabilities")
                        }
                    }
                    FlowRow(
                        horizontalArrangement = Arrangement.spacedBy(10.dp),
                        verticalArrangement = Arrangement.spacedBy(10.dp)
                    ) {
                        Button(onClick = {
                            val error = validateHttpUrl(gatewayUrl, "Hermes API URL")
                            if (error != null) {
                                status = error
                                return@Button
                            }

                            val healthUrl = "${hermesRoot(AppSettings(gatewayUrl = gatewayUrl.trim()))}/health"
                            status = "Test: $healthUrl"
                            scope.launch {
                                status = testGateway(healthUrl, gatewaySecret.ifBlank { loadGatewaySecret(context) })
                            }
                        }) {
                            Text("Test Hermes")
                        }
                        Button(onClick = {
                            deleteGatewaySecret(context)
                            gatewaySecret = ""
                            onReset()
                        }) {
                            Text("Reset")
                        }
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

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun SettingsPasswordField(label: String, value: String, onValueChange: (String) -> Unit) {
    TextField(
        modifier = Modifier.fillMaxWidth(),
        value = value,
        onValueChange = onValueChange,
        label = { Text(label, color = AppColors.Muted) },
        visualTransformation = PasswordVisualTransformation(),
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
    return validateHttpUrl(settings.gatewayUrl, "Hermes API URL")
        ?: validateRequired(settings.provider, "Provider")
        ?: validateHttpUrl(settings.inferenceEndpoint, "Endpoint API")
        ?: validateRequired(settings.preferredApi, "API preferita")
        ?: validateRequired(settings.model, "Modello")
        ?: validateRequired(settings.accessMode, "Accesso")
}

private fun validateWsUrl(value: String, label: String): String? {
    if (value.isBlank()) return "$label obbligatorio."

    return try {
        val uri = URI(value)
        val scheme = uri.scheme.orEmpty().lowercase()
        if ((scheme == "ws" || scheme == "wss") && uri.host != null) null else "$label deve essere URL ws/wss valido."
    } catch (_: Exception) {
        "$label deve essere URL ws/wss valido."
    }
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

private suspend fun sendChatRequest(
    settings: AppSettings,
    mode: String,
    prompt: String,
    history: List<ChatMessage>,
    conversationId: String?,
    previousResponseId: String?,
    apiKey: String?
): GatewayChatResult = withContext(Dispatchers.IO) {
    var lastError = "errore sconosciuto"

    if (supportsResponsesApi(settings, apiKey)) {
        try {
            val payload = JSONObject()
                .put("model", settings.model)
                .put("input", prompt)
                .put(
                    "instructions",
                    if (mode.equals("Agente", ignoreCase = true)) {
                        "Agisci come Hermes Agent operativo. Usa strumenti e memoria disponibili lato server e conserva un riepilogo chiaro delle azioni."
                    } else {
                        "Rispondi come assistente conversazionale Hermes."
                    }
                )
                .put("store", true)
                .put("conversation", conversationId ?: JSONObject.NULL)
                .put("previous_response_id", previousResponseId ?: JSONObject.NULL)
            val response = postJson("${settings.gatewayUrl.trimEnd('/')}/responses", payload, apiKey)
            if (response.first in 200..299) {
                val text = extractAssistantText(response.second)
                if (text.isNotBlank()) {
                    return@withContext GatewayChatResult(
                        text = text,
                        source = "Hermes",
                        statusMessage = "Risposta ricevuta da Hermes Responses API.",
                        usedFallback = false,
                        responseId = extractResponseId(response.second)
                    )
                }
                lastError = "Hermes Responses API raggiunta ma senza contenuto utile"
            } else {
                lastError = "Responses API HTTP ${response.first}: ${extractHumanError(response.second)}"
            }
        } catch (ex: Exception) {
            lastError = ex.message ?: ex.javaClass.simpleName
        }
    }

    try {
        val payload = JSONObject()
            .put("model", settings.model)
            .put("stream", false)
            .put("messages", JSONArray().apply {
                history.filter { !it.isAction }.forEach { message ->
                    put(
                        JSONObject()
                            .put("role", if (message.fromUser) "user" else "assistant")
                            .put("content", message.text)
                    )
                }
            })
        val response = postJson("${settings.gatewayUrl.trimEnd('/')}/chat/completions", payload, apiKey)
        if (response.first in 200..299) {
            val text = extractAssistantText(response.second)
            if (text.isNotBlank()) {
                return@withContext GatewayChatResult(
                    text = text,
                    source = "Hermes",
                    statusMessage = "Risposta ricevuta da Hermes Chat Completions.",
                    usedFallback = false,
                    responseId = extractResponseId(response.second)
                )
            }
            lastError = "Hermes Chat Completions raggiunta ma senza contenuto utile"
        } else {
            lastError = "Chat Completions HTTP ${response.first}: ${extractHumanError(response.second)}"
        }
    } catch (ex: Exception) {
        lastError = ex.message ?: ex.javaClass.simpleName
    }

    if (settings.demoMode) {
        GatewayChatResult(
            text = buildFallbackReply(settings, mode, lastError),
            source = "Fallback locale",
            statusMessage = "Hermes non disponibile, uso fallback locale: $lastError.",
            usedFallback = true
        )
    } else {
        GatewayChatResult(
            text = "Hermes non raggiungibile: $lastError.",
            source = "Errore Hermes",
            statusMessage = "Invio fallito: $lastError.",
            usedFallback = false
        )
    }
}

private suspend fun queueTaskRequest(settings: AppSettings, task: AgentTask, apiKey: String?): GatewayTaskResult = withContext(Dispatchers.IO) {
    val payload = JSONObject()
        .put("title", task.title)
        .put("instructions", task.detail)
        .put("detail", task.detail)
        .put("mode", task.mode)
        .put("requiresApproval", task.requiresApproval)
        .put("approvalRequired", task.requiresApproval)
        .put("model", settings.model)
        .put("provider", settings.provider)

    try {
        val response = postJson("${hermesRoot(settings)}/api/jobs", payload, apiKey)
        if (response.first in 200..299) {
            val remoteId = extractTaskId(response.second)
            val status = extractTaskStatus(response.second) ?: task.status
            return@withContext GatewayTaskResult(
                task.copy(
                    remoteId = remoteId,
                    mode = "Job",
                    status = status,
                    source = "Hermes Jobs",
                    updatedAt = System.currentTimeMillis()
                ),
                "Job creato su Hermes."
            )
        }

        val error = "HTTP ${response.first}: ${extractHumanError(response.second)}"
        return@withContext fallbackTaskResult(settings, task, "Creazione job fallita: $error")
    } catch (ex: Exception) {
        return@withContext fallbackTaskResult(settings, task, "Creazione job fallita: ${ex.message ?: ex.javaClass.simpleName}")
    }
}

private suspend fun updateTaskRequest(settings: AppSettings, task: AgentTask, action: String, apiKey: String?): GatewayTaskResult = withContext(Dispatchers.IO) {
    val targetStatus = when (action) {
        "run" -> "Run richiesto"
        "pause" -> "Pausa richiesta"
        else -> "Eliminato"
    }

    if (task.remoteId.isNullOrBlank()) {
        return@withContext GatewayTaskResult(
            task.copy(
                status = "$targetStatus locale",
                source = "Fallback locale",
                updatedAt = System.currentTimeMillis()
            ),
            "Job aggiornato in locale: $targetStatus."
        )
    }

    try {
        val url = if (action == "delete") {
            "${hermesRoot(settings)}/api/jobs/${task.remoteId}"
        } else {
            "${hermesRoot(settings)}/api/jobs/${task.remoteId}/$action"
        }
        val response = postJson(url, JSONObject(), apiKey, if (action == "delete") "DELETE" else "POST")
        if (response.first in 200..299) {
            return@withContext GatewayTaskResult(
                task.copy(
                    status = if (action == "delete") "Eliminato" else extractTaskStatus(response.second) ?: targetStatus,
                    source = "Hermes Jobs",
                    updatedAt = System.currentTimeMillis()
                ),
                "Job aggiornato su Hermes."
            )
        }

        val error = "HTTP ${response.first}: ${extractHumanError(response.second)}"
        return@withContext fallbackTaskResult(settings, task.copy(status = targetStatus), "Aggiornamento job fallito: $error")
    } catch (ex: Exception) {
        return@withContext fallbackTaskResult(settings, task.copy(status = targetStatus), "Aggiornamento job fallito: ${ex.message ?: ex.javaClass.simpleName}")
    }
}

private suspend fun loadServerSnapshot(settings: AppSettings, apiKey: String?): ServerSnapshot = withContext(Dispatchers.IO) {
    val healthUrl = "${hermesRoot(settings)}/health"
    val baseSnapshot = ServerSnapshot(
        gateway = settings.gatewayUrl,
        model = settings.model,
        providerDetail = "Provider: ${settings.provider} | API: ${settings.preferredApi}",
        inferenceEndpoint = settings.inferenceEndpoint,
        policy = settings.accessMode,
        statusMessage = if (settings.demoMode) "Fallback locale attivo. Provero' comunque a usare Hermes." else "Solo Hermes. Verifica lo stato del server."
    )

    try {
        val healthStatus = testGateway(healthUrl, apiKey)
        try {
            val body = httpGet("${hermesRoot(settings)}/health/detailed", apiKey)
            val json = JSONObject(body)
            ServerSnapshot(
                gateway = settings.gatewayUrl,
                model = json.extractString("model")
                    ?: json.extractNestedString("server", "model")
                    ?: json.extractNestedString("runtime", "model")
                    ?: settings.model,
                providerDetail = "Provider: ${json.extractString("provider") ?: json.extractNestedString("server", "provider") ?: settings.provider} | API: ${json.extractString("preferredApi") ?: json.extractString("api") ?: settings.preferredApi}",
                inferenceEndpoint = json.extractString("inferenceEndpoint")
                    ?: json.extractNestedString("server", "inferenceEndpoint")
                    ?: json.extractNestedString("runtime", "endpoint")
                    ?: settings.inferenceEndpoint,
                policy = json.extractString("accessMode")
                    ?: json.extractString("networkPolicy")
                    ?: json.extractNestedString("security", "networkPolicy")
                    ?: settings.accessMode,
                statusMessage = json.extractString("status")
                    ?: json.extractString("message")
                    ?: healthStatus
            )
        } catch (_: Exception) {
            baseSnapshot.copy(statusMessage = if (settings.demoMode) "$healthStatus Fallback locale attivo." else "$healthStatus Solo Hermes.")
        }
    } catch (ex: Exception) {
        baseSnapshot.copy(statusMessage = "Hermes non raggiungibile: ${ex.message ?: ex.javaClass.simpleName}")
    }
}

private suspend fun testGateway(healthUrl: String, apiKey: String?): String = withContext(Dispatchers.IO) {
    try {
        val connection = (URL(healthUrl).openConnection() as HttpURLConnection).apply {
            requestMethod = "GET"
            connectTimeout = 5_000
            readTimeout = 5_000
            setRequestProperty("Accept", "application/json")
            if (!apiKey.isNullOrBlank()) {
                setRequestProperty("Authorization", "Bearer ${apiKey.trim()}")
            }
        }

        connection.use {
            val code = it.responseCode
            if (code in 200..299) "Hermes raggiungibile." else "Hermes risponde: HTTP $code"
        }
    } catch (ex: Exception) {
        "Hermes non raggiungibile: ${ex.message ?: ex.javaClass.simpleName}"
    }
}

private suspend fun probeGatewayWs(settings: AppSettings, authSecret: String?): GatewayWsProbe = withContext(Dispatchers.IO) {
    val wsUrl = normalizeGatewayWsUrl(settings.gatewayWsUrl, settings.gatewayUrl)
    val error = validateWsUrl(wsUrl, "Gateway WebSocket URL")
    if (error != null) {
        return@withContext GatewayWsProbe(wsUrl, false, error, "Usa ws:// o wss://.")
    }

    try {
        val client = OkHttpClient.Builder().build()
        val responses = mutableListOf<String>()
        val messages = CompletableDeferred<MutableList<String>>()
        val requestBuilder = Request.Builder()
            .url(wsUrl)
            .header("User-Agent", "HermesHub-Android")
        if (!authSecret.isNullOrBlank()) {
            requestBuilder.header("Authorization", "Bearer ${authSecret.trim()}")
        }

        lateinit var socket: WebSocket
        socket = client.newWebSocket(
            requestBuilder.build(),
            object : WebSocketListener() {
                override fun onOpen(webSocket: WebSocket, response: Response) {
                    webSocket.send(buildConnectFrame(authSecret))
                }

                override fun onMessage(webSocket: WebSocket, text: String) {
                    responses += text
                    if (responses.size == 1) {
                        GATEWAY_WS_RPC_METHODS.forEach { method ->
                            webSocket.send(buildRpcFrame(method))
                        }
                    }
                    if (responses.size >= GATEWAY_WS_RPC_METHODS.size + 1 && !messages.isCompleted) {
                        messages.complete(responses)
                    }
                }

                override fun onFailure(webSocket: WebSocket, t: Throwable, response: Response?) {
                    if (!messages.isCompleted) {
                        messages.completeExceptionally(t)
                    }
                }

                override fun onClosed(webSocket: WebSocket, code: Int, reason: String) {
                    if (!messages.isCompleted) {
                        messages.completeExceptionally(IllegalStateException("Socket chiuso: $code $reason"))
                    }
                }
            }
        )

        val received = withTimeout(8_000) {
            messages.await()
        }
        socket.close(1000, "probe complete")
        client.dispatcher.executorService.shutdown()

        val hello = received.firstOrNull().orEmpty()
        val lines = received.drop(1).mapIndexed { index, frame ->
            "${GATEWAY_WS_RPC_METHODS[index]}: ${summarizeGatewayFrame(frame)}"
        }
        GatewayWsProbe(
            wsUrl = wsUrl,
            connected = true,
            status = "Gateway WS connesso.",
            detail = "Handshake: ${summarizeGatewayFrame(hello)}",
            capabilityLines = lines
        )
    } catch (ex: Exception) {
        GatewayWsProbe(
            wsUrl = wsUrl,
            connected = false,
            status = "Gateway WS non raggiungibile.",
            detail = ex.message ?: ex.javaClass.simpleName
        )
    }
}

private suspend fun hermesHttpCall(
    settings: AppSettings,
    apiKey: String?,
    target: String,
    rawPayload: String
): GatewayRpcCallResult = withContext(Dispatchers.IO) {
    val trimmed = target.trim()
    if (trimmed.isBlank()) {
        return@withContext GatewayRpcCallResult(target, false, "Endpoint obbligatorio.", "", "")
    }

    val parts = trimmed.split(" ", limit = 2).map { it.trim() }.filter { it.isNotBlank() }
    val verb = if (parts.size == 2) parts[0].uppercase() else if (rawPayload.isBlank()) "GET" else "POST"
    val path = if (parts.size == 2) parts[1] else trimmed
    val url = resolveHermesUrl(settings, path)

    try {
        val body = if (verb == "GET") {
            httpGet(url, apiKey)
        } else {
            val payload = if (rawPayload.isBlank()) JSONObject() else JSONObject(rawPayload)
            val response = postJson(url, payload, apiKey, verb)
            if (response.first in 200..299) response.second else "HTTP ${response.first}: ${extractHumanError(response.second)}"
        }

        GatewayRpcCallResult(
            method = "$verb $path",
            success = true,
            status = "Hermes risposta ricevuta.",
            rawJson = body,
            summary = body.limitText(180)
        )
    } catch (ex: Exception) {
        GatewayRpcCallResult(
            method = "$verb $path",
            success = false,
            status = "Hermes richiesta fallita.",
            rawJson = "",
            summary = ex.message ?: ex.javaClass.simpleName
        )
    }
}

private fun resolveHermesUrl(settings: AppSettings, path: String): String {
    if (path.startsWith("http://", ignoreCase = true) || path.startsWith("https://", ignoreCase = true)) return path
    val normalized = if (path.startsWith("/")) path else "/$path"
    return if (normalized.startsWith("/v1", ignoreCase = true) ||
        normalized.startsWith("/api/", ignoreCase = true) ||
        normalized.startsWith("/health", ignoreCase = true)
    ) {
        "${hermesRoot(settings)}$normalized"
    } else {
        "${settings.gatewayUrl.trimEnd('/')}$normalized"
    }
}

private suspend fun gatewayRpcCall(
    settings: AppSettings,
    authSecret: String?,
    method: String,
    rawParams: String
): GatewayRpcCallResult = withContext(Dispatchers.IO) {
    val targetMethod = method.trim()
    if (targetMethod.isBlank()) {
        return@withContext GatewayRpcCallResult(method, false, "Metodo RPC obbligatorio.", "", "")
    }

    val params = try {
        JSONObject(rawParams.ifBlank { "{}" })
    } catch (ex: Exception) {
        return@withContext GatewayRpcCallResult(targetMethod, false, "Parametri JSON non validi.", "", ex.message ?: ex.javaClass.simpleName)
    }

    val wsUrl = normalizeGatewayWsUrl(settings.gatewayWsUrl, settings.gatewayUrl)
    val error = validateWsUrl(wsUrl, "Gateway WebSocket URL")
    if (error != null) {
        return@withContext GatewayRpcCallResult(targetMethod, false, error, "", "Usa ws:// o wss://.")
    }

    try {
        val client = OkHttpClient.Builder().build()
        val responseMessage = CompletableDeferred<String>()
        val requestId = UUID.randomUUID().toString()
        val requestBuilder = Request.Builder()
            .url(wsUrl)
            .header("User-Agent", "HermesHub-Android")
        if (!authSecret.isNullOrBlank()) {
            requestBuilder.header("Authorization", "Bearer ${authSecret.trim()}")
        }

        lateinit var socket: WebSocket
        socket = client.newWebSocket(
            requestBuilder.build(),
            object : WebSocketListener() {
                private var rpcSent = false

                override fun onOpen(webSocket: WebSocket, response: Response) {
                    webSocket.send(buildConnectFrame(authSecret))
                }

                override fun onMessage(webSocket: WebSocket, text: String) {
                    val json = runCatching { JSONObject(text) }.getOrNull()
                    if (json?.optString("id") == requestId && !responseMessage.isCompleted) {
                        responseMessage.complete(text)
                    } else if (!rpcSent) {
                        rpcSent = true
                        webSocket.send(buildRpcFrame(targetMethod, params, requestId))
                    }
                }

                override fun onFailure(webSocket: WebSocket, t: Throwable, response: Response?) {
                    if (!responseMessage.isCompleted) {
                        responseMessage.completeExceptionally(t)
                    }
                }

                override fun onClosed(webSocket: WebSocket, code: Int, reason: String) {
                    if (!responseMessage.isCompleted) {
                        responseMessage.completeExceptionally(IllegalStateException("Socket chiuso: $code $reason"))
                    }
                }
            }
        )

        val raw = withTimeout(12_000) { responseMessage.await() }
        socket.close(1000, "rpc complete")
        client.dispatcher.executorService.shutdown()

        val json = JSONObject(raw)
        val failed = json.has("error")
        GatewayRpcCallResult(
            method = targetMethod,
            success = !failed,
            status = if (failed) "RPC errore." else "RPC completata.",
            rawJson = prettyJson(raw),
            summary = summarizeGatewayFrame(raw)
        )
    } catch (ex: Exception) {
        GatewayRpcCallResult(
            method = targetMethod,
            success = false,
            status = "RPC fallita.",
            rawJson = "",
            summary = ex.message ?: ex.javaClass.simpleName
        )
    }
}

private suspend fun adminBridgeCall(
    settings: AppSettings,
    token: String?,
    method: String,
    path: String,
    payload: JSONObject?
): GatewayRpcCallResult = withContext(Dispatchers.IO) {
    val baseUrl = settings.adminBridgeUrl.trimEnd('/')
    val error = validateHttpUrl(baseUrl, "Admin Bridge URL")
    if (error != null) {
        return@withContext GatewayRpcCallResult(path, false, error, "", "Usa http/https valido.")
    }

    try {
        val client = OkHttpClient.Builder().build()
        val builder = Request.Builder()
            .url("$baseUrl$path")
            .header("Accept", "application/json")
            .header("User-Agent", "HermesHub-Android")
        if (!token.isNullOrBlank()) {
            builder.header("Authorization", "Bearer ${token.trim()}")
        }

        val request = when (method.uppercase()) {
            "GET" -> builder.get().build()
            else -> builder
                .method(method.uppercase(), (payload ?: JSONObject()).toString().toRequestBody("application/json; charset=utf-8".toMediaType()))
                .build()
        }

        client.newCall(request).execute().use { response ->
            val body = response.body.string()
            GatewayRpcCallResult(
                method = path,
                success = response.isSuccessful,
                status = if (response.isSuccessful) "Admin Bridge OK." else "Admin Bridge HTTP ${response.code}.",
                rawJson = prettyJson(body),
                summary = summarizeGatewayFrame(body)
            )
        }
    } catch (ex: Exception) {
        GatewayRpcCallResult(
            method = path,
            success = false,
            status = "Admin Bridge fallito.",
            rawJson = "",
            summary = ex.message ?: ex.javaClass.simpleName
        )
    }
}

private suspend fun httpGet(url: String, apiKey: String? = null): String = withContext(Dispatchers.IO) {
    val connection = (URL(url).openConnection() as HttpURLConnection).apply {
        requestMethod = "GET"
        connectTimeout = 5_000
        readTimeout = 5_000
        setRequestProperty("Accept", "application/json")
        setRequestProperty("User-Agent", "HermesHub-Android")
        if (!apiKey.isNullOrBlank()) {
            setRequestProperty("Authorization", "Bearer ${apiKey.trim()}")
        }
    }

    connection.use {
        val stream = if (it.responseCode in 200..299) it.inputStream else it.errorStream
        stream?.bufferedReader()?.use { reader -> reader.readText() }.orEmpty()
    }
}

private suspend fun postJson(url: String, payload: JSONObject, apiKey: String? = null, method: String = "POST"): Pair<Int, String> = withContext(Dispatchers.IO) {
    val client = OkHttpClient.Builder().build()
    val builder = Request.Builder()
        .url(url)
        .header("Accept", "text/event-stream, application/json, text/plain")
        .header("User-Agent", "HermesHub-Android")
    if (!apiKey.isNullOrBlank()) {
        builder.header("Authorization", "Bearer ${apiKey.trim()}")
    }
    val body = payload.toString().toRequestBody("application/json; charset=utf-8".toMediaType())
    val request = when (method.uppercase()) {
        "PATCH" -> builder.patch(body).build()
        "DELETE" -> builder.delete().build()
        else -> builder.post(body).build()
    }

    client.newCall(request).execute().use { response ->
        response.code to response.body.string()
    }
}

private suspend fun supportsResponsesApi(settings: AppSettings, apiKey: String?): Boolean = withContext(Dispatchers.IO) {
    try {
        val body = httpGet("${settings.gatewayUrl.trimEnd('/')}/capabilities", apiKey)
        body.isBlank() || body.contains("responses", ignoreCase = true)
    } catch (_: Exception) {
        true
    }
}

private fun hermesRoot(settings: AppSettings): String {
    val api = settings.gatewayUrl.trimEnd('/')
    return if (api.endsWith("/v1", ignoreCase = true)) api.removeSuffix("/v1") else api
}

private val GATEWAY_WS_RPC_METHODS = listOf(
    "status",
    "system-presence",
    "models.status",
    "models.list",
    "plugins.list",
    "channels.status",
    "nodes.list",
    "exec.approvals.get"
)

private val OPERATOR_PRESETS = listOf(
    OperatorPreset("Dashboard", "Health", "GET /health", ""),
    OperatorPreset("Dashboard", "Health detailed", "GET /health/detailed", ""),
    OperatorPreset("Dashboard", "Capabilities", "GET /v1/capabilities", ""),
    OperatorPreset("Modelli", "Lista modelli", "GET /v1/models", ""),
    OperatorPreset("Runs", "Crea run", "POST /v1/runs", "{\"model\":\"hermes-agent\",\"input\":\"Controlla stato operativo e riassumi.\"}"),
    OperatorPreset("Jobs", "Lista jobs", "GET /api/jobs", ""),
    OperatorPreset("Jobs", "Crea job", "POST /api/jobs", "{\"title\":\"Controllo operativo\",\"instructions\":\"Controlla stato Hermes e segnala problemi.\"}")
)

private fun normalizeGatewayWsUrl(wsUrl: String, gatewayUrl: String): String {
    val candidate = wsUrl.ifBlank { gatewayUrl }.ifBlank { AppDefaults.gatewayWsUrl }
    return try {
        val uri = URI(candidate)
        val scheme = when (uri.scheme.orEmpty().lowercase()) {
            "https" -> "wss"
            "http" -> "ws"
            "ws", "wss" -> uri.scheme.lowercase()
            else -> "wss"
        }
        URI(
            scheme,
            uri.userInfo,
            uri.host,
            uri.port,
            uri.path.takeUnless { it.isNullOrBlank() },
            uri.query,
            uri.fragment
        ).toString().trimEnd('/')
    } catch (_: Exception) {
        AppDefaults.gatewayWsUrl
    }
}

private fun buildConnectFrame(authSecret: String?): String {
    return JSONObject()
        .put("type", "req")
        .put("id", UUID.randomUUID().toString())
        .put("method", "connect")
        .put(
            "params",
            JSONObject()
                .put("minProtocol", 3)
                .put("maxProtocol", 3)
                .put(
                    "client",
                    JSONObject()
                        .put("id", "chatclaw-android")
                        .put("version", "0.6.0")
                        .put("platform", "android")
                        .put("mode", "operator")
                )
                .put("role", "operator")
                .put("scopes", JSONArray(listOf("operator.read", "operator.write", "operator.approvals", "operator.pairing")))
                .put("caps", JSONArray())
                .put("commands", JSONArray())
                .put("permissions", JSONObject())
                .put("auth", if (authSecret.isNullOrBlank()) JSONObject.NULL else JSONObject().put("token", authSecret.trim()))
                .put("locale", "it-IT")
                .put("userAgent", "HermesHub-Android/0.6.0")
                .put(
                    "device",
                    JSONObject()
                        .put("id", "chatclaw-android")
                        .put("signedAt", System.currentTimeMillis())
                )
        )
        .toString()
}

private fun buildRpcFrame(method: String): String {
    return buildRpcFrame(method, JSONObject(), UUID.randomUUID().toString())
}

private fun buildRpcFrame(method: String, params: JSONObject, id: String): String {
    return JSONObject()
        .put("type", "req")
        .put("id", id)
        .put("method", method)
        .put("params", params)
        .toString()
}

private fun summarizeGatewayFrame(frame: String): String {
    return try {
        val json = JSONObject(frame)
        val error = json.opt("error")
        if (error != null) {
            "errore: ${extractGatewayText(error).ifBlank { error.toString() }}".limitText(180)
        } else {
            val value = json.opt("result") ?: json.opt("data") ?: json.opt("payload") ?: json.opt("params") ?: frame
            extractGatewayText(value).ifBlank { value.toString() }.limitText(180)
        }
    } catch (_: Exception) {
        frame.limitText(180)
    }
}

private fun extractGatewayText(value: Any?): String {
    return when (value) {
        is String -> value
        is JSONObject -> listOf("message", "status", "version", "name", "id")
            .firstNotNullOfOrNull { key -> value.optString(key).takeIf { it.isNotBlank() } }
            .orEmpty()
        else -> ""
    }
}

private fun prettyJson(raw: String): String {
    return try {
        JSONObject(raw).toString(2)
    } catch (_: Exception) {
        raw
    }
}

private fun String.limitText(maxLength: Int): String {
    return if (length <= maxLength) this else take(maxLength) + "..."
}

private fun String.jsonEscaped(): String {
    return replace("\\", "\\\\")
        .replace("\"", "\\\"")
        .replace("\r", "\\r")
        .replace("\n", "\\n")
}

private fun extractAssistantText(body: String): String {
    val trimmed = body.trim()
    if (trimmed.isBlank()) {
        return ""
    }

    if (trimmed.contains("data:", ignoreCase = true)) {
        val builder = StringBuilder()
        trimmed.lineSequence().forEach { rawLine ->
            val line = rawLine.trim()
            if (!line.startsWith("data:", ignoreCase = true)) return@forEach
            val payload = line.removePrefix("data:").trim()
            if (payload.isBlank() || payload == "[DONE]") return@forEach
            builder.append(extractAssistantText(payload))
        }
        return builder.toString().trim()
    }

    if (!trimmed.startsWith("{") && !trimmed.startsWith("[")) {
        return trimmed
    }

    return try {
        if (trimmed.startsWith("[")) {
            val array = JSONArray(trimmed)
            buildString {
                for (i in 0 until array.length()) {
                    append(extractJsonText(array.opt(i)))
                }
            }.trim()
        } else {
            extractJsonText(JSONObject(trimmed)).trim()
        }
    } catch (_: Exception) {
        trimmed
    }
}

private fun extractJsonText(value: Any?): String {
    return when (value) {
        is String -> value
        is JSONObject -> {
            listOf("text", "content", "message", "reply", "output_text").forEach { key ->
                val text = extractJsonText(value.opt(key))
                if (text.isNotBlank()) {
                    return text
                }
            }

            val choices = value.optJSONArray("choices")
            if (choices != null) {
                for (i in 0 until choices.length()) {
                    val text = extractJsonText(choices.opt(i))
                    if (text.isNotBlank()) {
                        return text
                    }
                }
            }

            listOf("delta", "choice", "data").forEach { key ->
                val text = extractJsonText(value.opt(key))
                if (text.isNotBlank()) {
                    return text
                }
            }
            ""
        }
        is JSONArray -> buildString {
            for (i in 0 until value.length()) {
                append(extractJsonText(value.opt(i)))
            }
        }
        else -> ""
    }
}

private fun extractTaskId(body: String): String? {
    return try {
        val json = JSONObject(body)
        json.extractString("id")
            ?: json.extractString("taskId")
            ?: json.extractString("job_id")
            ?: json.extractString("jobId")
            ?: json.extractNestedString("task", "id")
            ?: json.extractNestedString("job", "id")
    } catch (_: Exception) {
        null
    }
}

private fun extractTaskStatus(body: String): String? {
    return try {
        val json = JSONObject(body)
        json.extractString("status")
            ?: json.extractNestedString("task", "status")
            ?: json.extractNestedString("job", "status")
    } catch (_: Exception) {
        null
    }
}

private fun extractResponseId(body: String): String? {
    return try {
        val json = JSONObject(body)
        json.extractString("id")
            ?: json.extractString("response_id")
            ?: json.extractString("responseId")
            ?: json.extractNestedString("response", "id")
    } catch (_: Exception) {
        null
    }
}

private fun extractHumanError(body: String): String {
    val trimmed = body.trim()
    if (trimmed.isBlank()) {
        return "errore sconosciuto"
    }

    return try {
        val json = JSONObject(trimmed)
        json.extractString("error")
            ?: json.extractString("message")
            ?: json.extractString("detail")
            ?: trimmed
    } catch (_: Exception) {
        trimmed
    }
}

private fun JSONObject.extractString(key: String): String? {
    val value = opt(key)
    return when (value) {
        is String -> value.takeIf { it.isNotBlank() }
        is JSONObject, is JSONArray -> extractJsonText(value).takeIf { it.isNotBlank() }
        else -> null
    }
}

private fun JSONObject.extractNestedString(parentKey: String, childKey: String): String? {
    return optJSONObject(parentKey)?.extractString(childKey)
}

private fun buildFallbackReply(settings: AppSettings, mode: String, reason: String): String {
    val prefix = if (mode == "Agente") {
        "Hermes assente: preparo un job locale e tengo il contesto pronto."
    } else {
        "Hermes assente: uso la risposta locale di emergenza senza perdere la conversazione."
    }

    return "$prefix Preset: API ${settings.gatewayUrl}, modello ${settings.model}, protocollo ${settings.preferredApi}. Motivo: $reason."
}

private fun fallbackTaskResult(settings: AppSettings, task: AgentTask, message: String): GatewayTaskResult {
    return if (settings.demoMode) {
        GatewayTaskResult(
            task.copy(
                mode = "Locale",
                status = if (task.requiresApproval) "In attesa approvazione" else "Pronto",
                source = "Fallback locale",
                updatedAt = System.currentTimeMillis()
            ),
            "$message Salvo il task in locale."
        )
    } else {
        GatewayTaskResult(
            task.copy(
                status = "Errore Hermes",
                source = "Errore Hermes",
                updatedAt = System.currentTimeMillis()
            ),
            message
        )
    }
}

private fun replaceTask(tasks: MutableList<AgentTask>, updatedTask: AgentTask) {
    val index = tasks.indexOfFirst { it.id == updatedTask.id }
    if (index >= 0) {
        tasks[index] = updatedTask
    } else {
        tasks.add(0, updatedTask)
    }
}

private suspend fun checkGithubUpdate(localVersion: String): UpdateCheckResult = withContext(Dispatchers.IO) {
    try {
        val connection = (URL(AppDefaults.latestReleaseApi).openConnection() as HttpURLConnection).apply {
            requestMethod = "GET"
            connectTimeout = 10_000
            readTimeout = 10_000
            setRequestProperty("Accept", "application/vnd.github+json")
            setRequestProperty("User-Agent", "HermesHub-Android")
        }

        connection.use {
            val code = it.responseCode
            if (code !in 200..299) {
                return@withContext UpdateCheckResult(
                    hasUpdate = false,
                    latestVersion = null,
                    message = "Nessuna release GitHub trovata. Crea una release con tag vX.Y.Z e asset APK.",
                    releaseUrl = AppDefaults.releasesPage,
                    assetUrl = null
                )
            }

            val body = it.inputStream.bufferedReader().use { reader -> reader.readText() }
            val json = JSONObject(body)
            val latest = normalizeVersion(json.optString("tag_name"))
            val releaseUrl = json.optString("html_url", AppDefaults.releasesPage)
            val assetUrl = findReleaseAsset(json.optJSONArray("assets") ?: JSONArray(), ".apk")
            val hasUpdate = compareVersions(latest, localVersion) > 0
            var message = if (hasUpdate) {
                "Aggiornamento disponibile: $localVersion -> $latest."
            } else {
                "App aggiornata. Versione locale: $localVersion, GitHub: $latest."
            }
            if (hasUpdate && assetUrl == null) {
                message += " Release trovata, ma manca asset Android .apk."
            }

            UpdateCheckResult(
                hasUpdate = hasUpdate,
                latestVersion = latest,
                message = message,
                releaseUrl = releaseUrl,
                assetUrl = assetUrl
            )
        }
    } catch (ex: Exception) {
        UpdateCheckResult(
            hasUpdate = false,
            latestVersion = null,
            message = "Controllo update non riuscito: ${ex.message ?: ex.javaClass.simpleName}",
            releaseUrl = AppDefaults.releasesPage,
            assetUrl = null
        )
    }
}

private inline fun <T : HttpURLConnection, R> T.use(block: (T) -> R): R {
    return try {
        block(this)
    } finally {
        disconnect()
    }
}

private fun findReleaseAsset(assets: JSONArray, suffix: String): String? {
    for (i in 0 until assets.length()) {
        val asset = assets.getJSONObject(i)
        val name = asset.optString("name")
        if (name.endsWith(suffix, ignoreCase = true)) {
            return asset.optString("browser_download_url")
        }
    }

    return null
}

private fun compareVersions(latest: String, local: String): Int {
    val latestParts = parseVersionParts(latest)
    val localParts = parseVersionParts(local)
    for (i in 0 until maxOf(latestParts.size, localParts.size)) {
        val left = latestParts.getOrElse(i) { 0 }
        val right = localParts.getOrElse(i) { 0 }
        if (left != right) return left.compareTo(right)
    }

    return 0
}

private fun parseVersionParts(value: String): List<Int> {
    return normalizeVersion(value)
        .substringBefore('-')
        .substringBefore('+')
        .split('.')
        .map { it.toIntOrNull() ?: 0 }
}

private fun normalizeVersion(value: String): String {
    return value.trim().trimStart('v', 'V')
}

private fun downloadProgressLabel(progress: Float?, label: String): String {
    val safe = (progress ?: 0f).coerceIn(0f, 1f)
    val percent = "${(safe * 100).toInt()}%"
    return if (label.isBlank()) percent else "$percent  $label"
}

private suspend fun downloadUpdateApk(
    context: Context,
    assetUrl: String,
    version: String,
    onProgress: (Float, String, String) -> Unit
): File? = withContext(Dispatchers.IO) {
    val targetDirectory = context.getExternalFilesDir(null) ?: context.cacheDir
    val targetFile = File(targetDirectory, "HermesHub-${normalizeVersion(version)}.apk")
    val client = OkHttpClient.Builder().build()
    val request = Request.Builder()
        .url(assetUrl)
        .header("Accept", "application/octet-stream")
        .header("User-Agent", "HermesHub-Android")
        .build()

    try {
        client.newCall(request).execute().use { response ->
            if (!response.isSuccessful) {
                return@withContext null
            }

            val body = response.body
            val totalBytes = body.contentLength()
            var downloadedBytes = 0L
            var lastPercent = -1

            body.byteStream().use { input ->
                targetFile.outputStream().use { output ->
                    val buffer = ByteArray(DEFAULT_BUFFER_SIZE)
                    while (true) {
                        val read = input.read(buffer)
                        if (read < 0) {
                            break
                        }

                        output.write(buffer, 0, read)
                        downloadedBytes += read

                        if (totalBytes > 0) {
                            val progress = (downloadedBytes.toFloat() / totalBytes.toFloat()).coerceIn(0f, 1f)
                            val percent = (progress * 100).toInt()
                            if (percent != lastPercent) {
                                lastPercent = percent
                                val sizeLabel = if (totalBytes > 0L) {
                                    "${downloadedBytes.toReadableFileSize()} / ${totalBytes.toReadableFileSize()}"
                                } else {
                                    downloadedBytes.toReadableFileSize()
                                }
                                withContext(Dispatchers.Main) {
                                    onProgress(progress, "Scaricamento APK in corso... $percent%", sizeLabel)
                                }
                            }
                        }
                    }
                }
            }
        }

        withContext(Dispatchers.Main) {
            onProgress(1f, "Download completato. APK pronto per l'installazione.", targetFile.length().toReadableFileSize())
        }
        targetFile
    } catch (_: Exception) {
        if (targetFile.exists()) {
            targetFile.delete()
        }
        null
    }
}

private fun installDownloadedApk(context: Context, apkPath: String): String {
    val apkFile = File(apkPath)
    if (!apkFile.exists()) {
        return "APK non trovato. Riscarica l'aggiornamento."
    }

    if (!context.packageManager.canRequestPackageInstalls()) {
        openAndroidIntent(
            context,
            Intent(
                Settings.ACTION_MANAGE_UNKNOWN_APP_SOURCES,
                Uri.parse("package:${context.packageName}")
            )
        )
        return "Consenti a Hermes Hub di installare APK sconosciuti, poi premi di nuovo Aggiorna."
    }

    val contentUri = FileProvider.getUriForFile(context, "${context.packageName}.fileprovider", apkFile)
    val installIntent = Intent(Intent.ACTION_VIEW)
        .setDataAndType(contentUri, "application/vnd.android.package-archive")
        .addFlags(Intent.FLAG_ACTIVITY_NEW_TASK or Intent.FLAG_GRANT_READ_URI_PERMISSION)

    return if (openAndroidIntent(context, installIntent)) {
        "Installer Android aperto. Conferma l'aggiornamento."
    } else {
        "Impossibile aprire l'installer Android."
    }
}

private fun findDownloadedUpdateApk(context: Context, version: String): File? {
    val normalizedVersion = normalizeVersion(version)
    val targetDirectory = context.getExternalFilesDir(null) ?: context.cacheDir
    val targetFile = File(targetDirectory, "HermesHub-$normalizedVersion.apk")
    return targetFile.takeIf { it.exists() }
}

private fun Long.toReadableFileSize(): String {
    if (this <= 0L) {
        return "0 B"
    }

    val units = listOf("B", "KB", "MB", "GB")
    var size = this.toDouble()
    var unitIndex = 0
    while (size >= 1024 && unitIndex < units.lastIndex) {
        size /= 1024.0
        unitIndex++
    }

    return if (unitIndex == 0) {
        "${size.toLong()} ${units[unitIndex]}"
    } else {
        String.format(java.util.Locale.US, "%.1f %s", size, units[unitIndex])
    }
}

private fun loadSettings(context: Context): AppSettings {
    val prefs = migratePrefs(context, CURRENT_SETTINGS_PREFS, LEGACY_SETTINGS_PREFS)
    return AppSettings(
        gatewayUrl = prefs.getString("gatewayUrl", AppDefaults.gatewayUrl) ?: AppDefaults.gatewayUrl,
        gatewayWsUrl = prefs.getString("gatewayWsUrl", AppDefaults.gatewayWsUrl) ?: AppDefaults.gatewayWsUrl,
        adminBridgeUrl = prefs.getString("adminBridgeUrl", AppDefaults.adminBridgeUrl) ?: AppDefaults.adminBridgeUrl,
        provider = prefs.getString("provider", AppDefaults.provider) ?: AppDefaults.provider,
        inferenceEndpoint = prefs.getString("inferenceEndpoint", AppDefaults.inferenceEndpoint) ?: AppDefaults.inferenceEndpoint,
        preferredApi = prefs.getString("preferredApi", AppDefaults.preferredApi) ?: AppDefaults.preferredApi,
        model = prefs.getString("model", AppDefaults.model) ?: AppDefaults.model,
        accessMode = prefs.getString("accessMode", AppDefaults.accessMode) ?: AppDefaults.accessMode,
        demoMode = prefs.getBoolean("demoMode", true)
    )
}

private fun saveSettings(context: Context, settings: AppSettings) {
    context.getSharedPreferences(CURRENT_SETTINGS_PREFS, Context.MODE_PRIVATE)
        .edit()
        .putString("gatewayUrl", settings.gatewayUrl)
        .putString("gatewayWsUrl", settings.gatewayWsUrl)
        .putString("adminBridgeUrl", settings.adminBridgeUrl)
        .putString("provider", settings.provider)
        .putString("inferenceEndpoint", settings.inferenceEndpoint)
        .putString("preferredApi", settings.preferredApi)
        .putString("model", settings.model)
        .putString("accessMode", settings.accessMode)
        .putBoolean("demoMode", settings.demoMode)
        .apply()
}

private fun hasGatewaySecret(context: Context): Boolean {
    return !loadGatewaySecret(context).isNullOrBlank()
}

private fun loadGatewaySecret(context: Context): String? {
    val encoded = context.getSharedPreferences(CURRENT_SETTINGS_PREFS, Context.MODE_PRIVATE)
        .getString(GATEWAY_SECRET_PREF_KEY, null)
        ?: return null

    return try {
        val packed = Base64.decode(encoded, Base64.NO_WRAP)
        if (packed.size <= 12) return null
        val iv = packed.copyOfRange(0, 12)
        val ciphertext = packed.copyOfRange(12, packed.size)
        val cipher = Cipher.getInstance("AES/GCM/NoPadding")
        cipher.init(Cipher.DECRYPT_MODE, getOrCreateGatewaySecretKey(), GCMParameterSpec(128, iv))
        String(cipher.doFinal(ciphertext), Charsets.UTF_8)
    } catch (_: Exception) {
        null
    }
}

private fun saveGatewaySecret(context: Context, secret: String) {
    if (secret.isBlank()) return

    val cipher = Cipher.getInstance("AES/GCM/NoPadding")
    cipher.init(Cipher.ENCRYPT_MODE, getOrCreateGatewaySecretKey())
    val ciphertext = cipher.doFinal(secret.trim().toByteArray(Charsets.UTF_8))
    val packed = cipher.iv + ciphertext
    context.getSharedPreferences(CURRENT_SETTINGS_PREFS, Context.MODE_PRIVATE)
        .edit()
        .putString(GATEWAY_SECRET_PREF_KEY, Base64.encodeToString(packed, Base64.NO_WRAP))
        .apply()
}

private fun deleteGatewaySecret(context: Context) {
    context.getSharedPreferences(CURRENT_SETTINGS_PREFS, Context.MODE_PRIVATE)
        .edit()
        .remove(GATEWAY_SECRET_PREF_KEY)
        .apply()
}

private fun getOrCreateGatewaySecretKey(): SecretKey {
    val keyStore = KeyStore.getInstance("AndroidKeyStore").apply { load(null) }
    (keyStore.getEntry(GATEWAY_SECRET_KEY_ALIAS, null) as? KeyStore.SecretKeyEntry)?.secretKey?.let {
        return it
    }

    val keyGenerator = KeyGenerator.getInstance(KeyProperties.KEY_ALGORITHM_AES, "AndroidKeyStore")
    val spec = KeyGenParameterSpec.Builder(
        GATEWAY_SECRET_KEY_ALIAS,
        KeyProperties.PURPOSE_ENCRYPT or KeyProperties.PURPOSE_DECRYPT
    )
        .setBlockModes(KeyProperties.BLOCK_MODE_GCM)
        .setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE)
        .build()
    keyGenerator.init(spec)
    return keyGenerator.generateKey()
}

private fun loadArchiveItems(context: Context): List<ArchiveItem> {
    return loadConversations(context).map {
        ArchiveItem(
            id = it.id,
            title = it.title,
            kind = it.kind,
            description = it.description.ifBlank { "Ultimo aggiornamento locale." },
            prompt = it.prompt
        )
    }
}

private fun loadConversation(context: Context, id: String): LocalConversation? {
    return loadConversations(context).firstOrNull { it.id == id }
}

private fun saveConversationExchange(
    context: Context,
    conversationId: String?,
    mode: String,
    prompt: String,
    response: String,
    source: String,
    responseId: String? = null
): LocalConversation {
    val conversations = loadConversations(context).toMutableList()
    val index = conversations.indexOfFirst { it.id == conversationId }
    val now = System.currentTimeMillis()
    val newMessages = listOf(
        ChatMessage("Tu", prompt, fromUser = true),
        ChatMessage("Hermes", response, fromUser = false)
    )

    val conversation = if (index >= 0) {
        val current = conversations[index]
        current.copy(
            kind = if (mode == "Agente") "Task" else current.kind,
            description = if (mode == "Agente") "Conversazione agente via $source." else "Conversazione chat via $source.",
            prompt = prompt,
            updatedAt = now,
            messages = current.messages + newMessages,
            previousResponseId = responseId ?: current.previousResponseId
        )
    } else {
        LocalConversation(
            id = "conv_$now",
            title = makeTitle(prompt),
            kind = if (mode == "Agente") "Task" else "Chat",
            description = if (mode == "Agente") "Conversazione agente via $source." else "Conversazione chat via $source.",
            prompt = prompt,
            updatedAt = now,
            messages = newMessages,
            previousResponseId = responseId
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

private fun renameConversation(context: Context, id: String, newTitle: String): Boolean {
    if (newTitle.isBlank()) return false

    val conversations = loadConversations(context).toMutableList()
    val index = conversations.indexOfFirst { it.id == id }
    if (index < 0) return false

    conversations[index] = conversations[index].copy(title = newTitle, updatedAt = System.currentTimeMillis())
    saveConversations(context, conversations)
    return true
}

private fun deleteConversation(context: Context, id: String): Boolean {
    val conversations = loadConversations(context).toMutableList()
    val removed = conversations.removeAll { it.id == id }
    if (removed) {
        saveConversations(context, conversations)
    }
    return removed
}

private fun copyArchiveToClipboard(context: Context) {
    val clipboard = context.getSystemService(Context.CLIPBOARD_SERVICE) as ClipboardManager
    clipboard.setPrimaryClip(ClipData.newPlainText("chatclaw-archive", exportArchiveText(context)))
}

private fun exportArchiveText(context: Context): String {
    val conversations = loadConversations(context)
    if (conversations.isEmpty()) {
        return "Archivio Hermes Hub vuoto."
    }

    return conversations.joinToString("\n\n") { conversation ->
        val messages = conversation.messages.joinToString("\n") { message ->
            "${message.author}: ${message.text}"
        }
        "## ${conversation.title}\nTipo: ${conversation.kind}\nPrompt: ${conversation.prompt}\n$messages"
    }
}

private fun loadConversations(context: Context): List<LocalConversation> {
    val raw = migratePrefs(context, CURRENT_ARCHIVE_PREFS, LEGACY_ARCHIVE_PREFS).getString("items", "[]") ?: "[]"
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
                        messages = readMessages(obj.optJSONArray("messages") ?: JSONArray()),
                        previousResponseId = obj.optString("previousResponseId").takeIf { it.isNotBlank() }
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
                    .put("previousResponseId", conversation.previousResponseId ?: JSONObject.NULL)
                    .put("messages", writeMessages(conversation.messages))
            )
        }

    context.getSharedPreferences(CURRENT_ARCHIVE_PREFS, Context.MODE_PRIVATE)
        .edit()
        .putString("items", array.toString())
        .apply()
}

private fun loadTasks(context: Context): List<AgentTask> {
    val raw = migratePrefs(context, CURRENT_TASKS_PREFS, LEGACY_TASKS_PREFS).getString("items", "[]") ?: "[]"
    return try {
        val array = JSONArray(raw)
        buildList {
            for (i in 0 until array.length()) {
                val obj = array.getJSONObject(i)
                add(
                    AgentTask(
                        id = obj.optString("id"),
                        remoteId = obj.optString("remoteId").ifBlank { null },
                        title = obj.optString("title"),
                        mode = obj.optString("mode", "Locale"),
                        status = obj.optString("status", "Pronto"),
                        detail = obj.optString("detail"),
                        requiresApproval = obj.optBoolean("requiresApproval", true),
                        source = obj.optString("source", "Locale"),
                        updatedAt = obj.optLong("updatedAt")
                    )
                )
            }
        }.sortedByDescending { it.updatedAt }
    } catch (_: Exception) {
        emptyList()
    }
}

private fun saveTasks(context: Context, tasks: List<AgentTask>) {
    val array = JSONArray()
    tasks.sortedByDescending { it.updatedAt }
        .take(200)
        .forEach { task ->
            array.put(
                JSONObject()
                    .put("id", task.id)
                    .put("remoteId", task.remoteId)
                    .put("title", task.title)
                    .put("mode", task.mode)
                    .put("status", task.status)
                    .put("detail", task.detail)
                    .put("requiresApproval", task.requiresApproval)
                    .put("source", task.source)
                    .put("updatedAt", task.updatedAt)
            )
        }

    context.getSharedPreferences(CURRENT_TASKS_PREFS, Context.MODE_PRIVATE)
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

private fun appVersion(context: Context): String {
    return try {
        val info = context.packageManager.getPackageInfo(context.packageName, 0)
        info.versionName ?: "debug"
    } catch (_: Exception) {
        "debug"
    }
}

private fun openAndroidIntent(context: Context, intent: Intent): Boolean {
    return try {
        context.startActivity(intent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK))
        true
    } catch (_: Exception) {
        false
    }
}

private fun migratePrefs(context: Context, currentName: String, legacyName: String): SharedPreferences {
    val current = context.getSharedPreferences(currentName, Context.MODE_PRIVATE)
    if (current.all.isNotEmpty()) {
        return current
    }

    val legacy = context.getSharedPreferences(legacyName, Context.MODE_PRIVATE)
    if (legacy.all.isNotEmpty()) {
        val editor = current.edit()
        legacy.all.forEach { (key, value) ->
            when (value) {
                is String -> editor.putString(key, value)
                is Boolean -> editor.putBoolean(key, value)
                is Int -> editor.putInt(key, value)
                is Long -> editor.putLong(key, value)
                is Float -> editor.putFloat(key, value)
            }
        }
        editor.apply()
    }

    return current
}

private const val CURRENT_SETTINGS_PREFS = "chatclaw_settings"
private const val LEGACY_SETTINGS_PREFS = "nemoclaw_settings"
private const val CURRENT_ARCHIVE_PREFS = "chatclaw_archive"
private const val LEGACY_ARCHIVE_PREFS = "nemoclaw_archive"
private const val CURRENT_TASKS_PREFS = "chatclaw_tasks"
private const val LEGACY_TASKS_PREFS = "nemoclaw_tasks"
private const val GATEWAY_SECRET_PREF_KEY = "gatewaySecretCiphertext"
private const val GATEWAY_SECRET_KEY_ALIAS = "chatclaw_gateway_secret"

private object AppDefaults {
    const val gatewayUrl = "http://hermes.local:8642/v1"
    const val gatewayWsUrl = ""
    const val adminBridgeUrl = "http://hermes.local:8642"
    const val provider = "hermes-agent"
    const val inferenceEndpoint = "http://hermes.local:8642/v1"
    const val preferredApi = "openai-responses"
    const val model = "hermes-agent"
    const val accessMode = "Tailscale/LAN"
    const val releasesPage = "https://github.com/JackoPeru/app-interazione-nemoclaw/releases"
    const val latestReleaseApi = "https://api.github.com/repos/JackoPeru/app-interazione-nemoclaw/releases/latest"
}

private object AppColors {
    val Background = Color(0xFF0F1115)
    val Sidebar = Color(0xFF14171D)
    val Composer = Color(0xFF1A1E26)
    val Surface = Color(0xFF1A1E26)
    val Elevated = Color(0xFF232831)
    val AssistantBubble = Color(0xFF1F242E)
    val UserBubble = Color(0xFF7A3E00)
    val Muted = Color(0xFFA2ADBF)
    val Faint = Color(0xFF6B7585)
    val Accent = Color(0xFFF5A524)
    val NavIndicator = Color(0xFF4A351F)
    val Border = Color(0xFF232932)
}
