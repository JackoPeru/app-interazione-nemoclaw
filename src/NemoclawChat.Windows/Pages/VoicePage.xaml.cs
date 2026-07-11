using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using NemoclawChat_Windows.Services;
using System.Text;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.System;

namespace NemoclawChat_Windows.Pages;

public sealed partial class VoicePage : Page
{
    private readonly DispatcherTimer _timer = new();
    private readonly List<VoiceParticle> _particles = BuildParticles();
    private readonly List<Ellipse> _dots = [];
    private readonly List<ChatMessageRecord> _history = [];
    private readonly VoiceActivityRecorder _recorder = new();
    private readonly SolidColorBrush _particleBrush = new(Microsoft.UI.ColorHelper.FromArgb(245, 255, 128, 24));
    private readonly SolidColorBrush _hotBrush = new(Microsoft.UI.ColorHelper.FromArgb(255, 255, 218, 98));
    private readonly SolidColorBrush _glowBrush = new(Microsoft.UI.ColorHelper.FromArgb(92, 255, 105, 16));
    private CancellationTokenSource? _callCts;
    private Task? _callTask;
    private MediaPlayer? _voicePlayer;
    private DateTimeOffset _lastFrame = DateTimeOffset.Now;
    private DateTimeOffset _lastTranscriptAt = DateTimeOffset.MinValue;
    private VoiceCallPhase _phase = VoiceCallPhase.Idle;
    private double _time;
    private double _assembly;
    private bool _callActive;
    private bool _callReady;
    private string _lastTranscript = string.Empty;

    public VoicePage()
    {
        InitializeComponent();
        _timer.Interval = TimeSpan.FromMilliseconds(16);
        _timer.Tick += Timer_Tick;
        BuildVisuals();
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        Root.Focus(FocusState.Programmatic);
        _lastFrame = DateTimeOffset.Now;
        _timer.Start();
    }

    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
        _callActive = false;
        _callReady = false;
        _callCts?.Cancel();
        _recorder.Stop();
        _voicePlayer?.Dispose();
    }

    private async void CallButton_Click(object sender, RoutedEventArgs e)
    {
        if (_callActive)
        {
            await StopCallSessionAsync();
        }
        else
        {
            await StartCallSessionAsync();
        }
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (Frame.CanGoBack)
        {
            Frame.GoBack();
        }
        else
        {
            Frame.Navigate(typeof(HomePage));
        }
    }

    private void Root_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape)
        {
            Back_Click(sender, new RoutedEventArgs());
            e.Handled = true;
        }
    }

    private async Task StartCallSessionAsync()
    {
        _callCts?.Cancel();
        _callCts = new CancellationTokenSource();
        _callActive = true;
        _callReady = false;
        _lastTranscript = string.Empty;
        SetPhase(VoiceCallPhase.Connecting, "Connessione a Hermes...");
        SetCallVisual(true);

        try
        {
            var settings = AppSettingsStore.Load();
            await SpeechGatewayService.EnsureReadyAsync(settings, _callCts.Token).ConfigureAwait(true);
            if (!_callActive)
            {
                return;
            }
            _callReady = true;
            SetPhase(VoiceCallPhase.Listening, "Ti ascolto.");
            _callTask = ListenLoopAsync(_callCts.Token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _callActive = false;
            _callReady = false;
            SetPhase(VoiceCallPhase.Error, $"Voce non disponibile: {ex.Message}");
            SetCallVisual(false);
        }
    }

    private async Task StopCallSessionAsync()
    {
        _callActive = false;
        _callReady = false;
        _callCts?.Cancel();
        _recorder.Stop();
        _voicePlayer?.Dispose();
        _voicePlayer = null;
        var callTask = _callTask;
        _callTask = null;
        if (callTask is not null)
        {
            try { await callTask.ConfigureAwait(true); }
            catch (OperationCanceledException) { }
            catch { }
        }
        SetPhase(VoiceCallPhase.Idle, "Chiamata chiusa.");
        SetCallVisual(false);
    }

    private async Task ListenLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _callActive)
        {
            string? path = null;
            try
            {
                SetPhase(VoiceCallPhase.Listening, "Ti ascolto.");
                path = await _recorder.CaptureUtteranceAsync(cancellationToken).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                SetPhase(VoiceCallPhase.Thinking, "Trascrivo...");
                var settings = AppSettingsStore.Load();
                var text = await SpeechGatewayService.TranscribeFileAsync(settings, path, cancellationToken).ConfigureAwait(false);
                if (!ShouldSendTranscript(text))
                {
                    continue;
                }

                SetStatus($"Tu: {TrimForStatus(text)}");
                await SendVoiceTurnAsync(settings, text, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                SetPhase(VoiceCallPhase.Error, $"Errore voce: {ex.Message}");
                await Task.Delay(900, CancellationToken.None).ConfigureAwait(false);
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(path))
                {
                    TryDelete(path);
                }
            }
        }
    }

    private async Task SendVoiceTurnAsync(AppSettings settings, string prompt, CancellationToken cancellationToken)
    {
        var contextHistory = _history.TakeLast(10).ToList();
        _history.Add(new ChatMessageRecord("Tu", prompt, DateTimeOffset.Now));
        TrimHistory();

        var answer = new StringBuilder();
        var speechBuffer = new StringBuilder();
        Task playbackChain = Task.CompletedTask;
        var speechQueued = false;
        SetPhase(VoiceCallPhase.Thinking, "Hermes sta pensando...");

        await foreach (var ev in ChatStreamClient.StreamChatAsync(
                           settings,
                           "Chat",
                           $"Sei in una chiamata vocale. Rispondi subito in italiano, con tono naturale e frasi brevi. Niente markdown, elenchi o preamboli. La prima frase deve avere al massimo 8 parole. Utente: {prompt}",
                           contextHistory,
                           conversationId: null,
                           previousResponseId: null,
                           cancellationToken: cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            switch (ev)
            {
                case StreamTextDelta delta:
                    answer.Append(delta.Delta);
                    speechBuffer.Append(delta.Delta);
                    SetStatus($"Hermes: {TrimForStatus(answer.ToString())}");
                    foreach (var segment in DrainSpeechSegments(speechBuffer, flush: false))
                    {
                        if (!speechQueued)
                        {
                            speechQueued = true;
                            SetPhase(VoiceCallPhase.Speaking);
                        }
                        playbackChain = QueueSpeechSegmentAsync(playbackChain, settings, segment, cancellationToken);
                    }
                    break;
                case StreamDone done when !string.IsNullOrWhiteSpace(done.AccumulatedText):
                    answer.Clear();
                    answer.Append(done.AccumulatedText);
                    break;
                case StreamError error:
                    throw new InvalidOperationException(error.Message);
            }
        }

        foreach (var segment in DrainSpeechSegments(speechBuffer, flush: true))
        {
            if (!speechQueued)
            {
                speechQueued = true;
                SetPhase(VoiceCallPhase.Speaking);
            }
            playbackChain = QueueSpeechSegmentAsync(playbackChain, settings, segment, cancellationToken);
        }
        await playbackChain.ConfigureAwait(false);

        var finalText = answer.ToString().Trim();
        if (!string.IsNullOrWhiteSpace(finalText))
        {
            _history.Add(new ChatMessageRecord("Hermes", finalText, DateTimeOffset.Now));
            TrimHistory();
        }
        if (_callActive)
        {
            SetPhase(VoiceCallPhase.Listening, "Ti ascolto.");
        }
    }

    private Task QueueSpeechSegmentAsync(
        Task previous,
        AppSettings settings,
        string text,
        CancellationToken cancellationToken)
    {
        var synthesis = SpeechGatewayService.SynthesizeToFileAsync(settings, text, cancellationToken);
        return PlaySynthesizedAfterAsync(previous, synthesis, cancellationToken);
    }

    private async Task PlaySynthesizedAfterAsync(
        Task previous,
        Task<string> synthesis,
        CancellationToken cancellationToken)
    {
        var file = await synthesis.ConfigureAwait(false);
        try
        {
            await previous.ConfigureAwait(false);
            await PlayAudioFileAsync(file, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            TryDelete(file);
        }
    }

    private Task PlayAudioFileAsync(string filePath, CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!DispatcherQueue.TryEnqueue(() =>
        {
            _voicePlayer?.Dispose();
            var player = new MediaPlayer
            {
                Source = MediaSource.CreateFromUri(new Uri(filePath))
            };
            _voicePlayer = player;
            var finished = 0;
            CancellationTokenRegistration registration = default;

            void Complete(Exception? error = null, bool canceled = false)
            {
                if (Interlocked.Exchange(ref finished, 1) != 0)
                {
                    return;
                }
                registration.Dispose();
                player.Dispose();
                if (ReferenceEquals(_voicePlayer, player))
                {
                    _voicePlayer = null;
                }
                if (canceled)
                {
                    completion.TrySetCanceled(cancellationToken);
                }
                else if (error is not null)
                {
                    completion.TrySetException(error);
                }
                else
                {
                    completion.TrySetResult();
                }
            }

            player.MediaEnded += (_, _) => Complete();
            player.MediaFailed += (_, args) => Complete(new InvalidOperationException(args.ErrorMessage));
            if (cancellationToken.IsCancellationRequested)
            {
                Complete(canceled: true);
                return;
            }
            registration = cancellationToken.Register(() => DispatcherQueue.TryEnqueue(() => Complete(canceled: true)));
            if (Volatile.Read(ref finished) == 0)
            {
                player.Play();
            }
        }))
        {
            completion.TrySetException(new InvalidOperationException("Riproduzione audio non disponibile."));
        }
        return completion.Task;
    }

    private void ParticleCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateScene(0);

    private void Timer_Tick(object? sender, object e)
    {
        var now = DateTimeOffset.Now;
        var dt = Math.Min(0.05, (now - _lastFrame).TotalSeconds);
        _lastFrame = now;
        _time += dt;
        UpdateScene(dt);
    }

    private void BuildVisuals()
    {
        ParticleCanvas.Children.Clear();
        _dots.Clear();
        foreach (var particle in _particles)
        {
            var glow = new Ellipse
            {
                Fill = _glowBrush,
                Width = particle.Size * 8,
                Height = particle.Size * 8,
                Opacity = 0
            };
            var dot = new Ellipse
            {
                Fill = particle.Hot ? _hotBrush : _particleBrush,
                Width = particle.Size,
                Height = particle.Size,
                Opacity = 0.72,
                Tag = glow
            };
            _dots.Add(dot);
            ParticleCanvas.Children.Add(glow);
            ParticleCanvas.Children.Add(dot);
        }
    }

    private void UpdateScene(double dt)
    {
        var width = Math.Max(1, ParticleCanvas.ActualWidth);
        var height = Math.Max(1, ParticleCanvas.ActualHeight);
        var target = _callActive && _callReady ? 1.0 : 0.0;
        _assembly += (target - _assembly) * Math.Min(1, dt * (target > _assembly ? 4.2 : 3.8));
        var eased = EaseInOut(_assembly);
        var speaking = _phase == VoiceCallPhase.Speaking;
        var primary = speaking ? Math.Pow(Math.Max(0, Math.Sin(_time * 10.8)), 2) * 0.105 : 0;
        var secondary = speaking ? Math.Pow(Math.Max(0, Math.Sin(_time * 17.1 + 0.8)), 2) * 0.035 : 0;
        var breath = target > 0 ? Math.Sin(_time * 1.35) * 0.012 : 0;
        var scale = Math.Min(width, height) * (0.355 + breath + primary + secondary);
        var camera = 4.1;
        var spin = _time * (0.17 + eased * 0.38);
        var tilt = Math.Sin(_time * 0.31) * 0.16;

        for (var index = 0; index < _particles.Count; index++)
        {
            var particle = _particles[index];
            var idleX = particle.IdleX + Math.Sin(_time * particle.Speed + particle.Phase) * 0.14;
            var idleY = particle.IdleY + Math.Cos(_time * particle.Speed * 0.73 + particle.Phase) * 0.11;
            var idleZ = particle.IdleZ + Math.Sin(_time * particle.Speed * 0.41 + particle.Phase) * 0.19;
            var x = Lerp(idleX, particle.SphereX, eased);
            var y = Lerp(idleY, particle.SphereY, eased);
            var z = Lerp(idleZ, particle.SphereZ, eased);

            var cosSpin = Math.Cos(spin);
            var sinSpin = Math.Sin(spin);
            var spunX = x * cosSpin - z * sinSpin;
            var spunZ = x * sinSpin + z * cosSpin;
            var cosTilt = Math.Cos(tilt);
            var sinTilt = Math.Sin(tilt);
            var rotatedY = y * cosTilt - spunZ * sinTilt;
            var rotatedZ = y * sinTilt + spunZ * cosTilt;
            var perspective = camera / Math.Max(0.65, camera + rotatedZ);
            var screenX = width * 0.5 + spunX * scale * perspective;
            var screenY = height * 0.46 + rotatedY * scale * perspective;
            var dotSize = Math.Clamp(particle.Size * (0.7 + perspective * 0.55 + eased * 0.35), 1.1, 6.2);
            var dot = _dots[index];
            var glow = (Ellipse)dot.Tag;
            dot.Width = dotSize;
            dot.Height = dotSize;
            dot.Opacity = Math.Clamp(0.3 + perspective * 0.22 + eased * 0.28, 0.28, 0.98);
            glow.Width = dotSize * (4.8 + (primary + secondary) * 18);
            glow.Height = glow.Width;
            glow.Opacity = Math.Clamp(0.05 + eased * 0.09 + (primary + secondary) * 0.75, 0.04, 0.28);
            Canvas.SetLeft(dot, screenX - dot.Width / 2);
            Canvas.SetTop(dot, screenY - dot.Height / 2);
            Canvas.SetLeft(glow, screenX - glow.Width / 2);
            Canvas.SetTop(glow, screenY - glow.Height / 2);
        }
    }

    private bool ShouldSendTranscript(string text)
    {
        var clean = text.Trim();
        if (clean.Length < 2)
        {
            return false;
        }
        var normalized = clean.Trim('.', ',', '!', '?', ' ').ToLowerInvariant();
        if (normalized is "grazie" or "sottotitoli e revisione a cura di qtss" or "sottotitoli creati dalla comunita amara.org")
        {
            return false;
        }

        var now = DateTimeOffset.Now;
        if (string.Equals(clean, _lastTranscript, StringComparison.OrdinalIgnoreCase) &&
            (now - _lastTranscriptAt).TotalSeconds < 8)
        {
            return false;
        }
        _lastTranscript = clean;
        _lastTranscriptAt = now;
        return true;
    }

    private static List<string> DrainSpeechSegments(StringBuilder buffer, bool flush)
    {
        var segments = new List<string>();
        while (true)
        {
            var text = buffer.ToString();
            var cut = FindSpeechCut(text, flush);
            if (cut <= 0)
            {
                return segments;
            }
            var segment = text[..cut].Trim();
            buffer.Remove(0, cut);
            if (segment.Length >= 2)
            {
                segments.Add(segment);
            }
        }
    }

    private static int FindSpeechCut(string text, bool flush)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return -1;
        }
        var searchLimit = Math.Min(text.Length, 76);
        for (var index = searchLimit - 1; index >= 0; index--)
        {
            if ((text[index] == '.' || text[index] == '!' || text[index] == '?' || text[index] == '\n') && index >= 9)
            {
                return index + 1;
            }
        }
        if (text.Length > 46)
        {
            var soft = text.LastIndexOfAny(new[] { ',', ';', ':', ' ' }, Math.Min(text.Length - 1, 46));
            return soft > 25 ? soft + 1 : 46;
        }
        return flush ? text.Length : -1;
    }

    private void SetPhase(VoiceCallPhase phase, string? status = null)
    {
        _phase = phase;
        if (status is not null)
        {
            SetStatus(status);
        }
    }

    private void SetStatus(string text)
    {
        DispatcherQueue.TryEnqueue(() => StatusText.Text = text);
    }

    private void SetCallVisual(bool active)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            CallIcon.Glyph = active ? "\uE778" : "\uE717";
            CallButton.BorderBrush = new SolidColorBrush(active
                ? Microsoft.UI.Colors.OrangeRed
                : Microsoft.UI.ColorHelper.FromArgb(85, 255, 255, 255));
            CallButton.Background = new SolidColorBrush(active
                ? Microsoft.UI.ColorHelper.FromArgb(220, 139, 32, 32)
                : Microsoft.UI.ColorHelper.FromArgb(34, 255, 255, 255));
        });
    }

    private void TrimHistory()
    {
        while (_history.Count > 20)
        {
            _history.RemoveAt(0);
        }
    }

    private static List<VoiceParticle> BuildParticles()
    {
        var random = new Random(8642);
        var particles = new List<VoiceParticle>();
        for (var index = 0; index < 520; index++)
        {
            var theta = 2 * Math.PI * random.NextDouble();
            var phi = Math.Acos(2 * random.NextDouble() - 1);
            var radius = 0.91 + random.NextDouble() * 0.18;
            particles.Add(new VoiceParticle(
                (random.NextDouble() - 0.5) * 7.0,
                (random.NextDouble() - 0.5) * 4.8,
                (random.NextDouble() - 0.5) * 6.2,
                radius * Math.Sin(phi) * Math.Cos(theta),
                radius * Math.Cos(phi),
                radius * Math.Sin(phi) * Math.Sin(theta),
                0.42 + random.NextDouble() * 1.16,
                random.NextDouble() * Math.PI * 2,
                1.35 + random.NextDouble() * 2.4,
                random.NextDouble() > 0.72));
        }
        return particles;
    }

    private static string TrimForStatus(string text)
    {
        var clean = text.Replace("\r", " ").Replace("\n", " ").Trim();
        return clean.Length > 110 ? clean[..110] + "..." : clean;
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch { }
    }

    private static double EaseInOut(double value) => value * value * (3 - 2 * value);
    private static double Lerp(double start, double stop, double amount) => start + (stop - start) * amount;

    private enum VoiceCallPhase
    {
        Idle,
        Connecting,
        Listening,
        Thinking,
        Speaking,
        Error
    }

    private sealed record VoiceParticle(
        double IdleX,
        double IdleY,
        double IdleZ,
        double SphereX,
        double SphereY,
        double SphereZ,
        double Speed,
        double Phase,
        double Size,
        bool Hot);
}
