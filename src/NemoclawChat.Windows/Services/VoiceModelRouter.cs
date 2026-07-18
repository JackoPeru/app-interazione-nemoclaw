namespace NemoclawChat_Windows.Services;

public static class VoiceModelRouter
{
    public const string EscalationMarker = "[[HERMES_LARGE]]";

    private static readonly string[] LargeModelTriggers =
    [
        "esegui", "fai ", "crea ", "modifica ", "elimina ", "installa ", "scarica ",
        "aggiorna ", "avvia ", "ferma ", "riavvia ", "apri ", "chiudi ", "salva ",
        "invia ", "manda ", "cerca ", "verifica ", "controlla ", "analizza ",
        "pianifica ", "programma ", "accendi ", "spegni ", "ragiona ", "confronta ",
        "task", "server", "terminale", "terminal", "ssh", "file", "cartella", "repo",
        "repository", "codice", "build", "release", "commit", "cron", "automazione",
        "ricerca aggiornata", "notizie di oggi", "ultima versione", "in dettaglio"
    ];

    public static bool RequiresLargeModel(string prompt)
    {
        var normalized = $" {prompt.Trim().ToLowerInvariant()} ";
        return normalized.Length > 420 || LargeModelTriggers.Any(normalized.Contains);
    }

    public static bool RequestedEscalation(string response) =>
        response.Contains("HERMES_LARGE", StringComparison.OrdinalIgnoreCase);

    public static string BuildSmallPrompt(string prompt) =>
        $"""
        Sei Hermes Voce, modello rapido. Rispondi solo alla richiesta dopo Utente, in italiano, con una risposta breve e naturale, senza markdown o ragionamento visibile.
        Gestisci solo conversazione semplice e domande brevi. Se servono strumenti, azioni, file, server, dati aggiornati, pianificazione o ragionamento complesso, scrivi soltanto {EscalationMarker}. Non usare strumenti e non fingere di avere eseguito azioni.
        Utente: {prompt}
        """;

    public static string BuildLargePrompt(string prompt) =>
        $"""
        Sei in una chiamata vocale. Gestisci completamente richiesta, inclusi strumenti e task necessari. Rispondi in italiano con tono naturale. Comunica subito esito o prima azione utile; niente markdown o preamboli. Prima frase massimo 8 parole.
        Utente: {prompt}
        """;
}
