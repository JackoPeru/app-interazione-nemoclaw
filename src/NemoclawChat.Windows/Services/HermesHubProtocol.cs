namespace NemoclawChat_Windows.Services;

public static class HermesHubProtocol
{
    public static string Instructions(string mode)
    {
        var shared = """
            Stai ricevendo messaggi da Hermes Hub, client operativo mobile/desktop di Hermes Agent.
            Hermes Hub non e' un modello separato: deve usare la stessa memoria agente, gli stessi jobs e lo stesso profilo operativo disponibili anche da CLI Hermes.
            Sezioni app:
            - Chat: conversazione principale.
            - Video: feed personale di video generati su PC/Hermes; il telefono riceve stream_url/download_url, non file locali diretti.
            - News: feed personale di articoli/briefing con fonti e feedback utente.
            - Jobs/Runs: coda operativa Hermes e lavori programmati.
            - Archivio: storico locale dell'app, non memoria agente principale.
            Immagini in chat: usa visual_blocks image_gallery con media_url dal proxy Hermes /v1/media/...; vietati file://, data: e URL esterni diretti.
            """;

        if (mode.Equals("Agente", StringComparison.OrdinalIgnoreCase))
        {
            return shared + """

                Agisci come Hermes Agent operativo. Usa strumenti, memoria, jobs e filesystem disponibili lato server e conserva un riepilogo chiaro delle azioni.
                Memoria: app, CLI, jobs, Video e News devono contribuire alla stessa memoria agente/profilo Matteo quando l'informazione e' stabile o utile in futuro. Se esiste un tool di memoria, usalo. Se non esiste, conserva la preferenza nel riepilogo operativo e nel job/artifact server.
                Se l'utente chiede un video, articolo, cron, briefing o contenuto ricorrente, crea/aggiorna job o artifact lato Hermes con metadata workspace=video/news, cosi Hermes Hub puo' mostrarlo nella sezione corretta.
                Quando crei un output destinato a Video o News, produci anche un oggetto JSON compatto con: kind, title, summary, status, job_id, stream_url, download_url, sources.
                """;
        }

        return shared + """

            Rispondi come assistente conversazionale Hermes.
            Se l'utente esprime una preferenza stabile, un gusto editoriale, una regola di lavoro o una decisione di progetto, trattala come memoria agente condivisa e persistente usando gli strumenti/memoria disponibili lato Hermes. Non considerare la chat dell'app una memoria separata.
            Se l'utente chiede contenuti destinati a Video o News, dichiara chiaramente destinazione, titolo, stato job/artifact e prossimi passi.
            """;
    }

    public static object Metadata(AppSettings settings, string? workspace = null, string? source = null)
    {
        return new
        {
            client = "hermes-hub",
            client_surface = "windows-app",
            profile = "Matteo",
            workspace,
            source,
            memory_policy = new
            {
                scope = "shared-hermes-agent-memory",
                share_with_cli = true,
                use_server_memory_tools = true,
                do_not_create_app_only_memory = true
            },
            hub_sections = new
            {
                chat = "Conversazione principale Hermes Hub.",
                video = "Feed personale video: output resta sul PC/Hermes, app usa stream_url/download_url e feedback.",
                news = "Feed personale articoli: Hermes produce articoli con fonti, app salva feedback.",
                jobs = "Coda Hermes Jobs condivisa con CLI/server.",
                runs = "Runs operative Hermes."
            },
            visual_blocks = new
            {
                min_supported_version = VisualBlocksContract.Version,
                max_supported_version = VisualBlocksContract.Version,
                mode = settings.VisualBlocksMode,
                image_gallery = "supported via /v1/media proxy URLs only"
            }
        };
    }
}
