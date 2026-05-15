using System.Text.Json;
using System.Text.Json.Serialization;

namespace NemoclawChat_Windows.Services;

public static class VisualBlocksContract
{
    public const int Version = 1;
    public const int MaxBlocks = 20;
    public const int MaxPayloadBytes = 500 * 1024;
    public const int MaxTableColumns = 12;
    public const int MaxTableRows = 100;
    public const int MaxChartSeries = 8;
    public const int MaxChartPointsPerSeries = 200;
    public static readonly string[] VisualModes = ["auto", "always", "never"];
}

public sealed record VisualBlockRecord
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("caption")]
    public string? Caption { get; init; }

    [JsonPropertyName("text")]
    public string? Text { get; init; }

    [JsonPropertyName("language")]
    public string? Language { get; init; }

    [JsonPropertyName("filename")]
    public string? Filename { get; init; }

    [JsonPropertyName("code")]
    public string? Code { get; init; }

    [JsonPropertyName("highlight_lines")]
    public List<int> HighlightLines { get; init; } = [];

    [JsonPropertyName("columns")]
    public List<VisualTableColumn> Columns { get; init; } = [];

    [JsonPropertyName("rows")]
    public List<Dictionary<string, JsonElement>> Rows { get; init; } = [];

    [JsonPropertyName("chart_type")]
    public string? ChartType { get; init; }

    [JsonPropertyName("x_label")]
    public string? XLabel { get; init; }

    [JsonPropertyName("y_label")]
    public string? YLabel { get; init; }

    [JsonPropertyName("unit")]
    public string? Unit { get; init; }

    [JsonPropertyName("summary")]
    public string? Summary { get; init; }

    [JsonPropertyName("series")]
    public List<VisualChartSeries> Series { get; init; } = [];

    [JsonPropertyName("source_format")]
    public string? SourceFormat { get; init; }

    [JsonPropertyName("source")]
    public string? Source { get; init; }

    [JsonPropertyName("rendered_media_url")]
    public string? RenderedMediaUrl { get; init; }

    [JsonPropertyName("alt")]
    public string? Alt { get; init; }

    [JsonPropertyName("layout")]
    public string? Layout { get; init; }

    [JsonPropertyName("images")]
    public List<VisualGalleryImage> Images { get; init; } = [];

    [JsonPropertyName("variant")]
    public string? Variant { get; init; }
}

public sealed record VisualTableColumn
{
    [JsonPropertyName("key")]
    public string Key { get; init; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; init; } = string.Empty;

    [JsonPropertyName("align")]
    public string Align { get; init; } = "left";

    [JsonPropertyName("format")]
    public string Format { get; init; } = "text";

    [JsonPropertyName("sortable")]
    public bool Sortable { get; init; }
}

public sealed record VisualChartSeries
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("points")]
    public List<VisualChartPoint> Points { get; init; } = [];
}

public sealed record VisualChartPoint
{
    [JsonPropertyName("x")]
    public JsonElement X { get; init; }

    [JsonPropertyName("y")]
    public double Y { get; init; }
}

public sealed record VisualGalleryImage
{
    [JsonPropertyName("media_url")]
    public string MediaUrl { get; init; } = string.Empty;

    [JsonPropertyName("alt")]
    public string Alt { get; init; } = string.Empty;

    [JsonPropertyName("caption")]
    public string? Caption { get; init; }
}

public static class VisualBlockParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        MaxDepth = 16
    };
    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        MaxDepth = 16
    };

    private static readonly HashSet<string> BlockTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "markdown",
        "code",
        "table",
        "chart",
        "diagram",
        "image_gallery",
        "callout"
    };

    public static IReadOnlyList<VisualBlockRecord> ExtractFromResponse(string body)
    {
        if (string.IsNullOrWhiteSpace(body) || EncodingSize(body) > VisualBlocksContract.MaxPayloadBytes * 3)
        {
            return [];
        }

        var trimmed = body.Trim();
        if (!trimmed.StartsWith("{", StringComparison.Ordinal) && !trimmed.StartsWith("[", StringComparison.Ordinal))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(trimmed, DocumentOptions);
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("visual_blocks_version", out var version) &&
                version.ValueKind == JsonValueKind.Number &&
                version.GetInt32() != VisualBlocksContract.Version)
            {
                return [];
            }

            if (TryGetPropertyRecursive(root, "visual_blocks", 0, out var blocksElement) &&
                blocksElement.ValueKind == JsonValueKind.Array)
            {
                var raw = blocksElement.GetRawText();
                if (EncodingSize(raw) > VisualBlocksContract.MaxPayloadBytes)
                {
                    return [];
                }

                var blocks = JsonSerializer.Deserialize<List<VisualBlockRecord>>(raw, JsonOptions) ?? [];
                return blocks
                    .Where(IsValid)
                    .Take(VisualBlocksContract.MaxBlocks)
                    .ToList();
            }
        }
        catch
        {
            return [];
        }

        return [];
    }

    public static bool IsValid(VisualBlockRecord block)
    {
        if (string.IsNullOrWhiteSpace(block.Id) ||
            string.IsNullOrWhiteSpace(block.Type) ||
            !BlockTypes.Contains(block.Type))
        {
            return false;
        }

        return block.Type.ToLowerInvariant() switch
        {
            "markdown" => !string.IsNullOrWhiteSpace(block.Text),
            "code" => !string.IsNullOrWhiteSpace(block.Code) && IsAllowedCodeLanguage(block.Language),
            "table" => block.Columns.Count is > 0 and <= VisualBlocksContract.MaxTableColumns &&
                       block.Rows.Count <= VisualBlocksContract.MaxTableRows,
            "chart" => (block.ChartType is "bar" or "line") &&
                       !string.IsNullOrWhiteSpace(block.Summary) &&
                       block.Series.Count is > 0 and <= VisualBlocksContract.MaxChartSeries &&
                       block.Series.All(series => series.Points.Count is > 0 and <= VisualBlocksContract.MaxChartPointsPerSeries),
            "diagram" => block.SourceFormat == "mermaid" &&
                         !string.IsNullOrWhiteSpace(block.Source) &&
                         !string.IsNullOrWhiteSpace(block.Alt),
            "image_gallery" => block.Images.Count is > 0 and <= 12 &&
                               block.Images.All(image => !string.IsNullOrWhiteSpace(image.MediaUrl) && !string.IsNullOrWhiteSpace(image.Alt)),
            "callout" => block.Variant is "info" or "warning" or "error" or "success" &&
                         !string.IsNullOrWhiteSpace(block.Text),
            _ => false
        };
    }

    public static string JsonValueToText(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => string.Empty,
            _ => value.GetRawText()
        };
    }

    private static bool TryGetPropertyRecursive(JsonElement element, string name, int depth, out JsonElement value)
    {
        value = default;
        if (depth > 16)
        {
            return false;
        }
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty(name, out value))
            {
                return true;
            }

            foreach (var property in element.EnumerateObject())
            {
                if (TryGetPropertyRecursive(property.Value, name, depth + 1, out value))
                {
                    return true;
                }
            }
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (TryGetPropertyRecursive(item, name, depth + 1, out value))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsAllowedCodeLanguage(string? language)
    {
        return language is "plaintext" or "mermaid" or "powershell" or "bash" or "json" or "xml" or
            "csharp" or "kotlin" or "python" or "javascript" or "typescript" or "sql" or "yaml" or "markdown";
    }

    private static int EncodingSize(string value)
    {
        return System.Text.Encoding.UTF8.GetByteCount(value);
    }
}

public static class VisualBlockFixtures
{
    public static bool ShouldAttach(AppSettings settings, string prompt)
    {
        if (settings.VisualBlocksMode.Equals("never", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (settings.VisualBlocksMode.Equals("always", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return prompt.Contains("visual", StringComparison.OrdinalIgnoreCase) ||
               prompt.Contains("diagram", StringComparison.OrdinalIgnoreCase) ||
               prompt.Contains("grafico", StringComparison.OrdinalIgnoreCase) ||
               prompt.Contains("tabella", StringComparison.OrdinalIgnoreCase) ||
               prompt.Contains("spiegazione visiva", StringComparison.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<VisualBlockRecord> Create()
    {
        return
        [
            new VisualBlockRecord
            {
                Id = "fixture-markdown",
                Type = "markdown",
                Title = "Schema operativo",
                Text = "## Hermes Visual Blocks\n- Testo fallback sempre completo\n- Blocchi tipizzati validati\n- Renderer statici sicuri"
            },
            new VisualBlockRecord
            {
                Id = "fixture-code",
                Type = "code",
                Title = "Esempio payload",
                Language = "json",
                Filename = "visual-response.json",
                Code = "{\n  \"output_text\": \"Risposta completa.\",\n  \"visual_blocks_version\": 1\n}",
                HighlightLines = [2]
            },
            new VisualBlockRecord
            {
                Id = "fixture-table",
                Type = "table",
                Title = "Limiti v1",
                Columns =
                [
                    new VisualTableColumn { Key = "item", Label = "Elemento" },
                    new VisualTableColumn { Key = "limit", Label = "Limite", Align = "right" }
                ],
                Rows =
                [
                    Row(("item", "Blocchi"), ("limit", "20")),
                    Row(("item", "Payload"), ("limit", "500 KB")),
                    Row(("item", "Chart"), ("limit", "8x200"))
                ]
            },
            new VisualBlockRecord
            {
                Id = "fixture-chart",
                Type = "chart",
                Title = "Esempio chart",
                ChartType = "bar",
                XLabel = "Piattaforma",
                YLabel = "Copertura",
                Unit = "%",
                Summary = "Windows e Android usano lo stesso contratto Visual Blocks v1.",
                Series =
                [
                    new VisualChartSeries
                    {
                        Name = "Copertura",
                        Points =
                        [
                            Point("Windows", 100),
                            Point("Android", 100),
                            Point("Legacy fallback", 100)
                        ]
                    }
                ]
            },
            new VisualBlockRecord
            {
                Id = "fixture-diagram",
                Type = "diagram",
                Title = "Flusso",
                SourceFormat = "mermaid",
                Source = "graph TD; User-->HermesHub; HermesHub-->HermesAgent; HermesAgent-->VisualBlocks;",
                Alt = "Utente verso Hermes Hub, Hermes Agent e Visual Blocks"
            },
            new VisualBlockRecord
            {
                Id = "fixture-gallery",
                Type = "image_gallery",
                Title = "Media proxy",
                Layout = "grid",
                Images =
                [
                    new VisualGalleryImage { MediaUrl = "/v1/media/example.webp", Alt = "Esempio asset da proxy Hermes", Caption = "Placeholder proxy" }
                ]
            },
            new VisualBlockRecord
            {
                Id = "fixture-callout",
                Type = "callout",
                Variant = "info",
                Title = "Sicurezza",
                Text = "Niente HTML, niente JS, niente SVG client-side."
            }
        ];
    }

    private static Dictionary<string, JsonElement> Row(params (string Key, string Value)[] cells)
    {
        return cells.ToDictionary(cell => cell.Key, cell => JsonSerializer.SerializeToElement(cell.Value));
    }

    private static VisualChartPoint Point(string x, double y)
    {
        return new VisualChartPoint { X = JsonSerializer.SerializeToElement(x), Y = y };
    }
}
