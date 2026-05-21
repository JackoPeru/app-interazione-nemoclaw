# Hermes Hub vs Hermes Native

Audit del 2026-05-21 su rischio: Hermes Hub puo' castrare Hermes Agent se diventa orchestratore rigido invece di client operativo sottile.

Stato implementazione 2026-05-21: P0-P4 applicati a Windows + Android per default native, strict mode, raw events, Visual Blocks forward-compatible e context delegato. Gateway locale aggiornato per dichiarare Hermes Native, alias `POST /v1/hermes/native`, evento iniziale `hermes.native.protocol` e pass-through raw `hermes.*`; Linux helper/docs allineati per Ubuntu/vLLM.

## Tesi

Hermes Hub deve essere **superficie operativa** di Hermes Agent, non cervello alternativo.

Regola architetturale:

- Hermes decide modello, planner, memoria, strumenti, artifact, job, retry agentici e forma semantica dell'output.
- Hermes Gateway espone in modo stabile e sicuro cio' che Hermes produce.
- Hermes Hub visualizza, archivia snapshot UI, invia input utente e mostra stato operativo senza riscrivere troppo il comportamento.

## Evidenze nel codice

### 1. Protocollo API troppo deciso dal client

File:

- `src/NemoclawChat.Windows/Services/AppSettings.cs`
- `src/NemoclawChat.Android/app/src/main/java/com/nemoclaw/chat/MainActivity.kt`
- `src/NemoclawChat.Android/app/src/main/java/com/nemoclaw/chat/HermesAuth.kt`
- `src/NemoclawChat.Windows/Services/GatewayService.cs`
- `config/hermes-defaults.json`

Stato attuale:

- Windows/Android default: `openai-completions`.
- `config/hermes-defaults.json`: `openai-responses`.
- Responses-first avviene solo se `preferredApi=openai-responses` **e** modalita `Agente`.
- Modalita `Chat` forza di fatto Chat Completions.

Rischio:

- Se Hermes nativo usa Responses/Runs/eventi custom per planner, tool graph e memoria, il path default Chat Completions puo' ridurre Hermes a chatbot compatibile OpenAI.
- Divergenza config vs app rende difficile capire quale protocollo sia "vero".

Fix consigliato:

- Introdurre `preferredApi=hermes-native` come default futuro.
- `hermes-native` chiama endpoint/capability nativa Hermes se presente.
- Fallback a Responses/Chat Completions solo con status esplicito in UI.
- Allineare `config/hermes-defaults.json`, Android, Windows.

Implementato:

- Default `preferredApi=hermes-native` in Android, Windows e config.
- Strict native mode ON di default.
- Gateway dichiara `features.hermes_native=true`, `features.native_event_passthrough=true`, `features.raw_hermes_events=true`.
- Alias stabile `POST /v1/hermes/native` verso Responses/native transport.

### 2. History e contesto tagliati lato app

File:

- `src/NemoclawChat.Android/app/src/main/java/com/nemoclaw/chat/MainActivity.kt`

Stato attuale:

- Android invia `state.messages.takeLast(CHAT_HISTORY_MAX_MESSAGES)`.
- `CHAT_HISTORY_MAX_MESSAGES = 30`.
- Context meter locale assume finestra 32k e overhead fisso.

Rischio:

- Hermes non riceve pieno storico locale se serve recupero conversazionale.
- Client decide finestra utile invece di delegare a Hermes summarizer/memory/router.
- Buono per protezione prompt, ma cattivo come default "Hermes vero".

Fix consigliato:

- In `hermes-native`, inviare `conversation_id`, `previous_response_id`, metadata e delta utente; lasciare a Hermes recuperare/summarizzare memoria.
- Tenere cap locale solo come fallback compat Chat Completions.
- Mostrare nel context meter: `delegato a Hermes` quando il server gestisce il contesto.

### 3. Visual Blocks v1 troppo statico per output agentici futuri

File:

- `src/NemoclawChat.Windows/Services/VisualBlocks.cs`
- `src/NemoclawChat.Android/app/src/main/java/com/nemoclaw/chat/MainActivity.kt`
- `config/visual-blocks.schema.json`
- `docs/visual-blocks-schema.md`

Stato attuale:

- `visual_blocks_version = 1`.
- Max 20 blocchi, 500 KB payload.
- Tipi fissi: markdown, code, table, chart, diagram, image_gallery, media_file, callout.
- Parser scarta versioni non uguali a 1.

Rischio:

- Hermes potrebbe produrre artifact piu' ricchi: file tree, patch, diff, notebook, task graph, browser trace, multi-agent timeline, provenance, citations, workspace state.
- Hub oggi li perderebbe o li degraderebbe a testo.

Fix consigliato:

- Aggiungere `visual_blocks_version >= 1` con `unknown_block` fallback renderizzato come JSON compatto.
- Aggiungere `artifact_ref`, `task_graph`, `patch`, `file_tree`, `trace`, `citation_set`.
- Tenere limiti UI, ma non buttare dati non renderizzabili: salvare raw payload in archivio/debug.

### 4. Eventi Hermes filtrati in pochi tipi UI

File:

- `src/NemoclawChat.Windows/Services/ChatStream.cs`
- `src/NemoclawChat.Android/app/src/main/java/com/nemoclaw/chat/ChatStream.kt`

Stato attuale:

- Eventi normalizzati in TextDelta, ThinkingDelta, ToolCall, ToolResult, VisualBlocks, Usage, Status.
- Eventi custom non mappati rischiano invisibilita'.
- Windows ha cap SSE totale 50 MB; Android accumulo testo 2M char.

Rischio:

- Hermes nativo puo' emettere planner step, memory write, retrieval hit, model-call intermedia, approval request, artifact created, permission request.
- Se Hub non li conosce, l'utente vede meno di quello che Hermes sta facendo.

Fix consigliato:

- Aggiungere evento UI `RawHermesEvent(name, json, severity, displayHint)`.
- Activity panel deve mostrare eventi sconosciuti in sezione "Eventi Hermes".
- Archivio deve conservare raw event log separato da testo finale.

Implementato:

- Android/Windows conservano e mostrano raw SSE `hermes.*`.
- Gateway Responses invia `hermes.native.protocol` e inoltra tool/progress custom come eventi raw oltre alla forma OpenAI Responses compat.

### 5. Prompt/instructions del client possono sovrascrivere policy Hermes

File:

- `src/NemoclawChat.Windows/Services/HermesHubProtocol.cs`
- `src/NemoclawChat.Android/app/src/main/java/com/nemoclaw/chat/ChatStream.kt`

Stato attuale:

- Hub inietta istruzioni lunghe su memoria, video, news, visual blocks, media proxy.
- Alcune regole sono corrette per UI/sicurezza, ma sono formulate come system prompt ripetuto.

Rischio:

- Prompt client compete con prompt nativo Hermes.
- Hermes puo' spendere contesto su contratto UI invece che sul task.
- Regole operative app-specific possono distorcere agent planning.

Fix consigliato:

- Spostare contratti stabili in metadata/capabilities, non in system prompt lungo.
- Lasciare system prompt client minimo: identita superficie, richiesta utente, capability dichiarate.
- Hermes server traduce capability in policy interne.

### 6. Fallback automatici possono mascherare problemi reali

File:

- `src/NemoclawChat.Windows/Services/GatewayService.cs`
- `src/NemoclawChat.Android/app/src/main/java/com/nemoclaw/chat/ChatStream.kt`
- `src/NemoclawChat.Android/app/src/main/java/com/nemoclaw/chat/HermesAuth.kt`

Stato attuale:

- Auth prova key salvata, poi `hermes-hub`, poi no-auth.
- Responses fallisce -> Chat Completions.
- Demo mode e fallback locale sono off di default, ma codice esiste.

Rischio:

- In debug, utente puo' credere di usare Hermes nativo mentre sta usando compat/no-auth/fallback.
- Diagnosi "Hermes castrato" diventa difficile.

Fix consigliato:

- Banner protocollo effettivo: `Hermes Native`, `Responses compat`, `Chat Completions compat`, `No-auth compat`, `Fallback locale`.
- Log diagnostico per ogni fallback con motivo e endpoint.
- Setting `strict native mode`: nessun fallback automatico, errore esplicito.

Implementato:

- UI/diagnostica espongono protocollo effettivo/fallback.
- Strict native mode blocca fallback Chat Completions/no-auth sul path chat salvo disattivazione esplicita.

## Piano priorita

### P0 - Diagnosi visibile

- Aggiungere modalita `strict native mode`.
- Mostrare protocollo effettivo e fallback in chat/server diagnostics.
- Conservare raw SSE/event log in archivio debug.

### P1 - Hermes Native Mode

- Nuovo valore settings: `hermes-native`.
- Capability negotiation su `/v1/capabilities` o `/health/detailed`.
- Endpoint preferito: quello che Hermes dichiara come nativo, non deciso hardcoded dal client.
- Fallback compat solo se `strict native mode=false`.

### P2 - Eventi estensibili

- `RawHermesEvent` su Android/Windows.
- Renderer activity generico per eventi sconosciuti.
- Persistenza raw events per conversazione.

### P3 - Visual Blocks v2 compatibile

- Non scartare versioni future.
- `unknown_block` fallback.
- Nuovi blocchi per artifact agentici: patch, file tree, task graph, trace, citations.

### P4 - Context delegato a Hermes

- In native mode, client non manda full/capped history come fonte principale.
- Client manda id conversazione + input; Hermes recupera memoria/storia.
- Context meter legge uso reale da server.

## Decisione consigliata

Prossima release non dovrebbe aggiungere feature Video/News prima di questo strato.

Prima stabilire contratto:

```text
Hermes Hub = thin operational client.
Hermes Gateway = stable transport + security + media proxy.
Hermes Agent = brain, memory, planner, tools, artifacts.
```

Se questa separazione resta pulita, Hermes Hub amplifica Hermes.
Se Hub continua a decidere protocollo, prompt, output schema e fallback, Hermes Hub diventera' collo di bottiglia.
