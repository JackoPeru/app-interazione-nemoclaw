using System.Text;
using System.Text.Json;

namespace NemoclawChat_Windows.Services;

public sealed record AutomationDefinition(
    string TaskPrompt,
    string Condition,
    int TimeoutSeconds,
    int RetryCount,
    string NotificationTemplate,
    string ProjectId,
    string Dependencies);

public static class AutomationPromptCodec
{
    private const string Prefix = "<!-- HERMES_HUB_AUTOMATION_V1:";
    private const string Suffix = " -->";

    public static string Encode(AutomationDefinition definition)
    {
        var normalized = definition with
        {
            TaskPrompt = Limit(definition.TaskPrompt, 12_000),
            Condition = Limit(definition.Condition, 2_000),
            TimeoutSeconds = Math.Clamp(definition.TimeoutSeconds, 10, 86_400),
            RetryCount = Math.Clamp(definition.RetryCount, 0, 10),
            NotificationTemplate = Limit(definition.NotificationTemplate, 2_000),
            ProjectId = Limit(definition.ProjectId, 200),
            Dependencies = Limit(definition.Dependencies, 2_000)
        };
        var json = JsonSerializer.Serialize(normalized);
        var metadata = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
        return FormattableString.Invariant($"""
            {Prefix}{metadata}{Suffix}
            Regole Automation Studio Hermes Hub:
            - Condizione: {(string.IsNullOrWhiteSpace(normalized.Condition) ? "sempre" : normalized.Condition)}
            - Timeout massimo: {normalized.TimeoutSeconds} secondi
            - Retry massimi: {normalized.RetryCount}
            - Progetto: {(string.IsNullOrWhiteSpace(normalized.ProjectId) ? "nessuno" : normalized.ProjectId)}
            - Dipendenze job: {(string.IsNullOrWhiteSpace(normalized.Dependencies) ? "nessuna" : normalized.Dependencies)}
            - Notifica finale: {(string.IsNullOrWhiteSpace(normalized.NotificationTemplate) ? "riepilogo standard" : normalized.NotificationTemplate)}
            Valuta condizione prima di agire. Se falsa, termina senza mutazioni e registra esito skipped. Rispetta timeout, retry, dipendenze e invia notifica tramite /v1/hub/notifications.

            Attività:
            {normalized.TaskPrompt}
            """);
    }

    public static AutomationDefinition Decode(string prompt)
    {
        if (!string.IsNullOrWhiteSpace(prompt) && prompt.StartsWith(Prefix, StringComparison.Ordinal))
        {
            var end = prompt.IndexOf(Suffix, Prefix.Length, StringComparison.Ordinal);
            if (end > Prefix.Length)
            {
                try
                {
                    var encoded = prompt[Prefix.Length..end];
                    var json = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
                    return JsonSerializer.Deserialize<AutomationDefinition>(json) ?? Empty(prompt);
                }
                catch (Exception ex) when (ex is FormatException or JsonException)
                {
                }
            }
        }

        return Empty(prompt);
    }

    private static AutomationDefinition Empty(string prompt) => new(prompt, string.Empty, 900, 0, string.Empty, string.Empty, string.Empty);

    private static string Limit(string? value, int max) => (value ?? string.Empty).Trim() is var text && text.Length > max ? text[..max] : text;
}
