using System.Text;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using NemoclawChat_Windows.Services;
using Windows.Foundation;
using Windows.UI;

namespace NemoclawChat_Windows.Pages;

internal sealed class StreamingBubble
{
    private readonly Page _page;
    private readonly Panel _host;
    private readonly ScrollViewer _scroll;
    private readonly StackPanel _content;
    private readonly TextBlock _thinkingLabel;
    private readonly Expander _thinkingExpander;
    private readonly TextBlock _thinkingText;
    private readonly StackPanel _toolCallsPanel;
    private readonly TextBlock _assistantText;
    private readonly TextBlock _statsText;
    private readonly LinearGradientBrush _shimmerBrush;
    private readonly DispatcherTimer _shimmerTimer;
    private readonly Dictionary<string, ToolCallView> _toolViews = new();
    private readonly StringBuilder _thinkingBuilder = new();
    private readonly StringBuilder _textBuilder = new();
    private bool _hasThinking;
    private bool _hasText;
    private double _shimmerPhase;
    private DateTime _started = DateTime.UtcNow;

    public StreamingBubble(Page page, Panel host, ScrollViewer scroll)
    {
        _page = page;
        _host = host;
        _scroll = scroll;
        _content = new StackPanel { Spacing = 10 };

        _content.Children.Add(new TextBlock
        {
            Text = "Hermes",
            FontSize = 12,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Colors.White)
        });

        _shimmerBrush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0.5),
            EndPoint = new Point(1, 0.5)
        };
        _shimmerBrush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(0xFF, 0x6F, 0x78, 0x88), Offset = 0.0 });
        _shimmerBrush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF), Offset = 0.5 });
        _shimmerBrush.GradientStops.Add(new GradientStop { Color = Color.FromArgb(0xFF, 0x6F, 0x78, 0x88), Offset = 1.0 });

        _thinkingLabel = new TextBlock
        {
            Text = "Sto pensando",
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = _shimmerBrush
        };

        _thinkingText = new TextBlock
        {
            Text = string.Empty,
            Foreground = (Brush)Application.Current.Resources["MutedTextBrush"],
            TextWrapping = TextWrapping.WrapWholeWords,
            FontSize = 12
        };

        _thinkingExpander = new Expander
        {
            Header = _thinkingLabel,
            Content = new ScrollViewer
            {
                MaxHeight = 220,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = _thinkingText
            },
            Background = new SolidColorBrush(Colors.Transparent),
            BorderThickness = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Visibility = Visibility.Collapsed
        };

        _content.Children.Add(_thinkingExpander);

        _toolCallsPanel = new StackPanel { Spacing = 8 };
        _content.Children.Add(_toolCallsPanel);

        _assistantText = new TextBlock
        {
            Text = string.Empty,
            TextWrapping = TextWrapping.WrapWholeWords,
            Foreground = new SolidColorBrush(Colors.White),
            Visibility = Visibility.Collapsed
        };
        _content.Children.Add(_assistantText);

        _statsText = new TextBlock
        {
            Text = string.Empty,
            FontSize = 11,
            Foreground = (Brush)Application.Current.Resources["MutedTextBrush"],
            Visibility = Visibility.Collapsed
        };
        _content.Children.Add(_statsText);

        var bubble = new Border
        {
            MaxWidth = 720,
            Padding = new Thickness(18, 14, 18, 14),
            CornerRadius = new CornerRadius(20),
            Background = (Brush)Application.Current.Resources["AssistantBubbleBrush"],
            BorderBrush = (Brush)Application.Current.Resources["BorderBrushSoft"],
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = _content
        };

        _host.Children.Add(bubble);
        _ = _scroll.ChangeView(null, _scroll.ScrollableHeight, null);

        _shimmerTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
        _shimmerTimer.Tick += (_, _) => AdvanceShimmer();
        _shimmerTimer.Start();
    }

    private void AdvanceShimmer()
    {
        _shimmerPhase += 0.025;
        if (_shimmerPhase > 1.5)
        {
            _shimmerPhase = -0.5;
        }
        _shimmerBrush.StartPoint = new Point(_shimmerPhase - 0.5, 0.5);
        _shimmerBrush.EndPoint = new Point(_shimmerPhase + 0.5, 0.5);
    }

    public void AppendText(string delta)
    {
        if (string.IsNullOrEmpty(delta))
        {
            return;
        }
        _textBuilder.Append(delta);
        _assistantText.Text = _textBuilder.ToString();
        if (!_hasText)
        {
            _hasText = true;
            _assistantText.Visibility = Visibility.Visible;
            // First text token: thinking phase done, freeze shimmer label
            if (_hasThinking)
            {
                FreezeThinkingLabel();
            }
            else
            {
                _thinkingExpander.Visibility = Visibility.Collapsed;
            }
        }
        ScheduleScroll();
    }

    public void AppendThinking(string delta)
    {
        if (string.IsNullOrEmpty(delta))
        {
            return;
        }
        _thinkingBuilder.Append(delta);
        _thinkingText.Text = _thinkingBuilder.ToString();
        _hasThinking = true;
        _thinkingExpander.Visibility = Visibility.Visible;
        ScheduleScroll();
    }

    private void FreezeThinkingLabel()
    {
        _shimmerTimer.Stop();
        var elapsed = (DateTime.UtcNow - _started).TotalSeconds;
        _thinkingLabel.Foreground = (Brush)Application.Current.Resources["MutedTextBrush"];
        _thinkingLabel.Text = elapsed >= 1
            ? $"Pensato per {elapsed:0.#}s"
            : "Ragionamento";
    }

    public void StartToolCall(string id, string name)
    {
        if (_toolViews.ContainsKey(id))
        {
            return;
        }

        var argsText = new TextBlock
        {
            Text = string.Empty,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 11,
            Foreground = (Brush)Application.Current.Resources["MutedTextBrush"],
            TextWrapping = TextWrapping.Wrap,
            Visibility = Visibility.Collapsed
        };
        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        header.Children.Add(new FontIcon { Glyph = "", FontSize = 14, Foreground = (Brush)Application.Current.Resources["AccentBrush"] });
        header.Children.Add(new TextBlock
        {
            Text = $"Tool · {name}",
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            FontSize = 12,
            Foreground = new SolidColorBrush(Colors.White)
        });
        var statusText = new TextBlock
        {
            Text = "in esecuzione…",
            FontSize = 11,
            Foreground = (Brush)Application.Current.Resources["MutedTextBrush"]
        };
        header.Children.Add(statusText);

        var panel = new StackPanel { Spacing = 4 };
        panel.Children.Add(header);
        panel.Children.Add(argsText);

        var border = new Border
        {
            Padding = new Thickness(12, 8, 12, 8),
            CornerRadius = new CornerRadius(12),
            Background = (Brush)Application.Current.Resources["SurfaceBrush"],
            BorderBrush = (Brush)Application.Current.Resources["BorderBrushSoft"],
            BorderThickness = new Thickness(1),
            Child = panel
        };

        _toolCallsPanel.Children.Add(border);
        _toolViews[id] = new ToolCallView(border, argsText, statusText, new StringBuilder());
        ScheduleScroll();
    }

    public void AppendToolCallArguments(string id, string delta)
    {
        if (string.IsNullOrEmpty(delta))
        {
            return;
        }
        if (!_toolViews.TryGetValue(id, out var view))
        {
            StartToolCall(id, "tool");
            view = _toolViews[id];
        }
        view.Args.Append(delta);
        view.ArgsBlock.Text = view.Args.ToString();
        view.ArgsBlock.Visibility = Visibility.Visible;
        ScheduleScroll();
    }

    public void EndToolCall(string id)
    {
        if (_toolViews.TryGetValue(id, out var view))
        {
            view.StatusBlock.Text = "completato";
        }
    }

    public void AddToolResult(string? id, string? name, string output)
    {
        if (!string.IsNullOrWhiteSpace(id) && _toolViews.TryGetValue(id!, out var view))
        {
            view.StatusBlock.Text = "risultato pronto";
            var result = new TextBlock
            {
                Text = output,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                Foreground = new SolidColorBrush(Colors.White),
                TextWrapping = TextWrapping.Wrap
            };
            ((StackPanel)((Border)view.Container).Child).Children.Add(result);
        }
        else
        {
            var resultBorder = new Border
            {
                Padding = new Thickness(12, 8, 12, 8),
                CornerRadius = new CornerRadius(12),
                Background = (Brush)Application.Current.Resources["SurfaceBrush"],
                BorderBrush = (Brush)Application.Current.Resources["BorderBrushSoft"],
                BorderThickness = new Thickness(1),
                Child = new TextBlock
                {
                    Text = $"Tool · {name ?? "risultato"}\n{output}",
                    Foreground = new SolidColorBrush(Colors.White),
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 12
                }
            };
            _toolCallsPanel.Children.Add(resultBorder);
        }
        ScheduleScroll();
    }

    public void SetVisualBlocks(IReadOnlyList<VisualBlockRecord> blocks, Func<VisualBlockRecord, UIElement> renderer)
    {
        if (blocks is null || blocks.Count == 0)
        {
            return;
        }
        foreach (var block in blocks.Where(VisualBlockParser.IsValid))
        {
            _content.Children.Add(renderer(block));
        }
        ScheduleScroll();
    }

    public void Complete(ChatStreamStats stats)
    {
        if (_hasThinking && _shimmerTimer.IsEnabled)
        {
            FreezeThinkingLabel();
        }
        else if (!_hasThinking)
        {
            _thinkingExpander.Visibility = Visibility.Collapsed;
            _shimmerTimer.Stop();
        }

        if (!_hasText && _textBuilder.Length > 0)
        {
            _assistantText.Visibility = Visibility.Visible;
            _hasText = true;
        }

        var parts = new List<string>();
        if (stats.TimeToFirstTokenMs is { } ttft && ttft > 0)
        {
            parts.Add($"TTFT {ttft:0}ms");
        }
        if (stats.TokensPerSecond is { } tps && tps > 0)
        {
            parts.Add($"{tps:0.0} t/s");
        }
        if (stats.TokensOut is { } toks && toks > 0)
        {
            parts.Add($"{toks} tok");
        }
        if (stats.PromptTokens is { } pt && pt > 0)
        {
            parts.Add($"prompt {pt}");
        }
        if (stats.TotalMs is { } total && total > 0)
        {
            parts.Add($"{total / 1000.0:0.0}s");
        }
        if (parts.Count > 0)
        {
            _statsText.Text = string.Join("  ·  ", parts);
            _statsText.Visibility = Visibility.Visible;
        }
        ScheduleScroll();
    }

    public void StopShimmer()
    {
        if (_shimmerTimer.IsEnabled)
        {
            _shimmerTimer.Stop();
        }
    }

    private void ScheduleScroll()
    {
        _ = _scroll.ChangeView(null, _scroll.ScrollableHeight, null, true);
    }

    private sealed record ToolCallView(Border Container, TextBlock ArgsBlock, TextBlock StatusBlock, StringBuilder Args);
}
