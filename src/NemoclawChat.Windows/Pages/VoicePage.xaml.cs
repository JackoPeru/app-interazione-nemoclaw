using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.System;

namespace NemoclawChat_Windows.Pages;

public sealed partial class VoicePage : Page
{
    private readonly DispatcherTimer _timer = new();
    private readonly List<VoiceParticle> _particles = BuildParticles();
    private readonly List<Ellipse> _dots = [];
    private readonly List<LineDef> _wireDefs = BuildWireDefs();
    private readonly List<Line> _wireLines = [];
    private readonly SolidColorBrush _dotBrush = new(Microsoft.UI.ColorHelper.FromArgb(255, 255, 139, 24));
    private readonly SolidColorBrush _glowBrush = new(Microsoft.UI.ColorHelper.FromArgb(72, 255, 96, 0));
    private readonly SolidColorBrush _wireBrush = new(Microsoft.UI.ColorHelper.FromArgb(255, 255, 122, 0));
    private DateTimeOffset _lastFrame = DateTimeOffset.Now;
    private double _time;
    private double _progress;
    private bool _assembled;

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
    }

    private void Root_Tapped(object sender, TappedRoutedEventArgs e)
    {
        _assembled = true;
    }

    private void Root_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        _assembled = false;
    }

    private void Root_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape && Frame.CanGoBack)
        {
            Frame.GoBack();
            e.Handled = true;
        }
    }

    private void ParticleCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateScene(0);
    }

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
        foreach (var lineDef in _wireDefs)
        {
            var line = new Line
            {
                Stroke = _wireBrush,
                StrokeThickness = lineDef.Weight,
                Opacity = 0
            };
            _wireLines.Add(line);
            ParticleCanvas.Children.Add(line);
        }

        foreach (var particle in _particles)
        {
            var glow = new Ellipse
            {
                Fill = _glowBrush,
                Width = particle.Size * 7,
                Height = particle.Size * 7,
                Opacity = 0.15
            };
            var dot = new Ellipse
            {
                Fill = _dotBrush,
                Width = particle.Size * 2.4,
                Height = particle.Size * 2.4,
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
        var w = Math.Max(1, ParticleCanvas.ActualWidth);
        var h = Math.Max(1, ParticleCanvas.ActualHeight);
        var target = _assembled ? 1.0 : 0.0;
        var rate = _assembled ? 8.4 : 6.1;
        _progress += (target - _progress) * Math.Min(1.0, dt * rate);
        if (Math.Abs(target - _progress) < 0.002) _progress = target;
        var eased = EaseOutCubic(_progress);
        var scale = Math.Clamp(Math.Min(w, h) / 920.0, 0.7, 1.35);

        for (var i = 0; i < _wireLines.Count; i++)
        {
            var def = _wireDefs[i];
            var line = _wireLines[i];
            line.X1 = def.X1 * w;
            line.Y1 = def.Y1 * h;
            line.X2 = def.X2 * w;
            line.Y2 = def.Y2 * h;
            line.StrokeThickness = def.Weight * scale;
            line.Opacity = eased * eased * def.Alpha * (0.68 + Math.Abs(Math.Sin(_time * 2.4 + def.Phase)) * 0.32);
        }

        for (var i = 0; i < _particles.Count; i++)
        {
            var p = _particles[i];
            var dot = _dots[i];
            var glow = (Ellipse)dot.Tag;
            var idleX = p.IdleX + Math.Sin((_time * p.Speed + p.Phase) * Math.PI * 2) * (0.014 + p.Size * 0.0018);
            var idleY = p.IdleY + Math.Cos((_time * p.Speed * 0.72 + p.Phase) * Math.PI * 2) * 0.018;
            var targetX = p.TargetX + Math.Sin((_time * 2.1 + p.Phase) * Math.PI * 2) * 0.0028;
            var targetY = p.TargetY + Math.Cos((_time * 1.7 + p.Phase) * Math.PI * 2) * 0.0032;
            var x = Lerp(idleX, targetX, eased) * w;
            var y = Lerp(idleY, targetY, eased) * h;
            var dotSize = (p.Size + eased * (p.Anchor ? 1.8 : 0.75)) * scale;
            dot.Width = dotSize * 2.2;
            dot.Height = dotSize * 2.2;
            dot.Opacity = Math.Clamp(0.22 + eased * 0.56 + (p.Anchor ? 0.16 : 0), 0.16, 0.94);
            glow.Width = dotSize * 7.2;
            glow.Height = dotSize * 7.2;
            glow.Opacity = Math.Clamp(0.12 + eased * 0.2, 0.08, 0.36);
            Canvas.SetLeft(dot, x - dot.Width / 2);
            Canvas.SetTop(dot, y - dot.Height / 2);
            Canvas.SetLeft(glow, x - glow.Width / 2);
            Canvas.SetTop(glow, y - glow.Height / 2);
        }
    }

    private static List<VoiceParticle> BuildParticles()
    {
        var random = new Random(8642);
        var targets = BuildTargets(random);
        return targets.OrderBy(_ => random.Next()).Select((target, index) => new VoiceParticle(
            random.NextDouble(),
            random.NextDouble(),
            target.X,
            target.Y,
            0.35 + random.NextDouble() * 1.2,
            random.NextDouble(),
            0.9 + random.NextDouble() * 1.6,
            index % 41 == 0)).ToList();
    }

    private static List<Point> BuildTargets(Random random)
    {
        var points = new List<Point>();
        void Add(double x, double y) => points.Add(new Point(Math.Clamp(x, 0.04, 0.96), Math.Clamp(y, 0.04, 0.96)));
        void Ellipse(double cx, double cy, double rx, double ry, int count, double start = 0, double end = Math.PI * 2)
        {
            for (var i = 0; i < count; i++)
            {
                var a = start + (end - start) * i / Math.Max(1, count - 1);
                Add(cx + Math.Cos(a) * rx, cy + Math.Sin(a) * ry);
            }
        }
        void Line(double x1, double y1, double x2, double y2, int count)
        {
            for (var i = 0; i < count; i++)
            {
                var t = i / (double)Math.Max(1, count - 1);
                Add(Lerp(x1, x2, t), Lerp(y1, y2, t));
            }
        }

        Ellipse(0.5, 0.48, 0.16, 0.21, 90);
        Ellipse(0.5, 0.39, 0.22, 0.08, 80, Math.PI, Math.PI * 2);
        Line(0.31, 0.39, 0.69, 0.39, 54);
        Line(0.5, 0.25, 0.5, 0.39, 24);
        Ellipse(0.43, 0.46, 0.045, 0.016, 28);
        Ellipse(0.57, 0.46, 0.045, 0.016, 28);
        Line(0.5, 0.47, 0.48, 0.54, 18);
        Line(0.48, 0.54, 0.52, 0.54, 12);
        Ellipse(0.5, 0.58, 0.055, 0.018, 26, Math.PI * 0.12, Math.PI * 0.88);
        Line(0.34, 0.66, 0.23, 0.88, 34);
        Line(0.66, 0.66, 0.77, 0.88, 34);
        Line(0.25, 0.76, 0.75, 0.76, 58);
        Line(0.18, 0.83, 0.82, 0.83, 58);
        for (var wing = 0; wing < 4; wing++)
        {
            var offset = wing * 0.025;
            Ellipse(0.25 + offset, 0.36 + offset * 0.8, 0.12 + offset, 0.20 - offset * 0.4, 32, Math.PI * 0.75, Math.PI * 1.62);
            Ellipse(0.75 - offset, 0.36 + offset * 0.8, 0.12 + offset, 0.20 - offset * 0.4, 32, Math.PI * 1.38, Math.PI * 2.25);
        }
        for (var i = 0; i < 130; i++)
        {
            var x = 0.5 + (random.NextDouble() - 0.5) * 0.34;
            var y = 0.48 + (random.NextDouble() - 0.5) * 0.42;
            var face = Math.Pow((x - 0.5) / 0.17, 2) + Math.Pow((y - 0.48) / 0.22, 2);
            if (face <= 1) Add(x, y);
        }
        for (var i = 0; i < 120; i++)
        {
            Add(0.22 + random.NextDouble() * 0.56, 0.64 + random.NextDouble() * 0.28);
        }
        return points;
    }

    private static List<LineDef> BuildWireDefs()
    {
        var defs = new List<LineDef>();
        void AddLine(double x1, double y1, double x2, double y2, double alpha = 1, double weight = 1.1)
        {
            defs.Add(new LineDef(x1, y1, x2, y2, alpha, weight, defs.Count * 0.17));
        }
        void Ellipse(double cx, double cy, double rx, double ry, int count, double start = 0, double end = Math.PI * 2, double alpha = 1)
        {
            Point? previous = null;
            for (var i = 0; i < count; i++)
            {
                var t = i / (double)Math.Max(1, count - 1);
                var a = start + (end - start) * t;
                var next = new Point(cx + Math.Cos(a) * rx, cy + Math.Sin(a) * ry);
                if (previous is Point p) AddLine(p.X, p.Y, next.X, next.Y, alpha);
                previous = next;
            }
        }

        Ellipse(0.5, 0.48, 0.16, 0.21, 54, alpha: 0.74);
        Ellipse(0.5, 0.39, 0.22, 0.08, 48, Math.PI, Math.PI * 2, 0.82);
        AddLine(0.31, 0.39, 0.69, 0.39, 0.9);
        AddLine(0.5, 0.25, 0.5, 0.39, 0.45);
        Ellipse(0.43, 0.46, 0.045, 0.016, 18, alpha: 0.92);
        Ellipse(0.57, 0.46, 0.045, 0.016, 18, alpha: 0.92);
        AddLine(0.5, 0.47, 0.48, 0.54, 0.42);
        AddLine(0.48, 0.54, 0.52, 0.54, 0.42);
        Ellipse(0.5, 0.58, 0.055, 0.018, 18, Math.PI * 0.12, Math.PI * 0.88, 0.52);
        for (var wing = 0; wing < 4; wing++)
        {
            var offset = wing * 0.025;
            Ellipse(0.25 + offset, 0.36 + offset * 0.8, 0.12 + offset, 0.20 - offset * 0.4, 28, Math.PI * 0.75, Math.PI * 1.62, 0.58);
            Ellipse(0.75 - offset, 0.36 + offset * 0.8, 0.12 + offset, 0.20 - offset * 0.4, 28, Math.PI * 1.38, Math.PI * 2.25, 0.58);
        }
        AddLine(0.34, 0.66, 0.23, 0.88, 0.36);
        AddLine(0.66, 0.66, 0.77, 0.88, 0.36);
        AddLine(0.25, 0.76, 0.75, 0.76, 0.3);
        AddLine(0.18, 0.83, 0.82, 0.83, 0.26);
        return defs;
    }

    private static double EaseOutCubic(double t) => 1 - Math.Pow(1 - t, 3);
    private static double Lerp(double start, double stop, double amount) => start + (stop - start) * amount;

    private sealed record VoiceParticle(double IdleX, double IdleY, double TargetX, double TargetY, double Speed, double Phase, double Size, bool Anchor);
    private sealed record LineDef(double X1, double Y1, double X2, double Y2, double Alpha, double Weight, double Phase);
}
