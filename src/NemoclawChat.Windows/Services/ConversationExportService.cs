using System.Net;
using System.Text;
using System.Text.Json;

namespace NemoclawChat_Windows.Services;

public static class ConversationExportService
{
    private static readonly JsonSerializerOptions ExportJsonOptions = new() { WriteIndented = true };
    private static readonly JsonSerializerOptions ImportJsonOptions = new() { PropertyNameCaseInsensitive = true };
    public static byte[] Export(ConversationRecord conversation, string format) => format.ToLowerInvariant() switch
    {
        "json" => Encoding.UTF8.GetBytes(JsonSerializer.Serialize(conversation, ExportJsonOptions)),
        "html" => Encoding.UTF8.GetBytes(ToHtml(conversation)),
        "pdf" => ToPdf(conversation),
        _ => Encoding.UTF8.GetBytes(ToMarkdown(conversation))
    };

    public static ConversationRecord? ImportJson(string json)
    {
        try { var item = JsonSerializer.Deserialize<ConversationRecord>(json, ImportJsonOptions); if (item is null) return null; item.Id = $"import_{Guid.NewGuid():N}"; item.UpdatedAt = DateTimeOffset.Now; item.DeletedAt = null; return item; }
        catch (JsonException) { return null; }
    }

    public static ConversationRecord ImportMarkdown(string markdown, string title)
    {
        var messages = new List<ChatMessageRecord>(); string author = "Import"; var buffer = new StringBuilder();
        void Flush() { if (buffer.Length == 0) return; messages.Add(new(author, buffer.ToString().Trim(), DateTimeOffset.Now)); buffer.Clear(); }
        foreach (var line in markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n')) { if (line.StartsWith("## ", StringComparison.Ordinal)) { Flush(); author = line[3..].Trim(); } else buffer.AppendLine(line); }
        Flush(); return new ConversationRecord { Id = $"import_{Guid.NewGuid():N}", Title = title, Kind = "Chat", Description = "Importata da Markdown.", UpdatedAt = DateTimeOffset.Now, Messages = messages };
    }

    private static string ToMarkdown(ConversationRecord item) { var builder = new StringBuilder().Append("# ").AppendLine(item.Title).AppendLine().AppendLine(item.Summary); foreach (var message in item.Messages) builder.Append("## ").AppendLine(message.Author).AppendLine().AppendLine(message.Text).AppendLine(); return builder.ToString(); }
    private static string ToHtml(ConversationRecord item) { var body = string.Join("", item.Messages.Select(message => $"<article><h2>{WebUtility.HtmlEncode(message.Author)}</h2><pre>{WebUtility.HtmlEncode(message.Text)}</pre></article>")); return $"<!doctype html><html><meta charset=\"utf-8\"><title>{WebUtility.HtmlEncode(item.Title)}</title><style>body{{font:16px Segoe UI,sans-serif;max-width:900px;margin:40px auto}}pre{{white-space:pre-wrap}}</style><h1>{WebUtility.HtmlEncode(item.Title)}</h1><p>{WebUtility.HtmlEncode(item.Summary)}</p>{body}</html>"; }
    private static byte[] ToPdf(ConversationRecord item)
    {
        var lines = ToMarkdown(item).Replace("\r", "", StringComparison.Ordinal).Split('\n').Select(line => new string(line.Select(ch => ch is >= ' ' and <= '~' ? ch : '?').ToArray())).Take(55);
        var content = new StringBuilder("BT /F1 10 Tf 50 790 Td 13 TL ");
        foreach (var line in lines)
        {
            content.Append('(').Append(line.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("(", "\\(", StringComparison.Ordinal).Replace(")", "\\)", StringComparison.Ordinal)).Append(") Tj T* ");
        }
        content.Append("ET");
        var stream = content.ToString();
        var objects = new[] { "<< /Type /Catalog /Pages 2 0 R >>", "<< /Type /Pages /Kids [3 0 R] /Count 1 >>", "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>", "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>", $"<< /Length {Encoding.ASCII.GetByteCount(stream)} >>\nstream\n{stream}\nendstream" };
        var pdf = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        for (var index = 0; index < objects.Length; index++)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(pdf.ToString()));
            pdf.Append(index + 1).Append(" 0 obj\n").Append(objects[index]).Append("\nendobj\n");
        }
        var xref = Encoding.ASCII.GetByteCount(pdf.ToString());
        pdf.Append("xref\n0 6\n0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            pdf.Append(offset.ToString("D10", System.Globalization.CultureInfo.InvariantCulture)).Append(" 00000 n \n");
        }
        pdf.Append("trailer << /Size 6 /Root 1 0 R >>\nstartxref\n").Append(xref).Append("\n%%EOF");
        return Encoding.ASCII.GetBytes(pdf.ToString());
    }
}
