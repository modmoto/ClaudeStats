using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using ClaudeStats.Core;

namespace ClaudeStats;

/// <summary>Zeichnet das Tray-Icon: links 5-Stunden-Ring, rechts Wochen-Ring.</summary>
public static class RingIconRenderer
{
    private const double Width = 40;
    private const double Height = 22;

    /// <param name="fiveHourRemaining">Rest in Prozent (0–100) oder null, wenn unbekannt.</param>
    /// <param name="weekRemaining">Rest in Prozent (0–100) oder null, wenn unbekannt.</param>
    public static WindowIcon Render(double? fiveHourRemaining, double? weekRemaining)
    {
        var visual = new RingsVisual(fiveHourRemaining, weekRemaining) { Width = Width, Height = Height };
        visual.Measure(new Size(Width, Height));
        visual.Arrange(new Rect(0, 0, Width, Height));

        // 2x für Retina-Displays; die Menüleiste skaliert auf Höhe herunter.
        var bitmap = new RenderTargetBitmap(new PixelSize((int)Width * 2, (int)Height * 2), new Vector(192, 192));
        bitmap.Render(visual);
        return new WindowIcon(bitmap);
    }

    private sealed class RingsVisual(double? five, double? week) : Control
    {
        private const double Radius = 8;
        private const double Thickness = 3;

        public override void Render(DrawingContext context)
        {
            DrawRing(context, new Point(11, 11), five);
            DrawRing(context, new Point(29, 11), week);
        }

        private static void DrawRing(DrawingContext context, Point center, double? remaining)
        {
            var track = new Pen(new SolidColorBrush(Color.FromArgb(70, 128, 128, 128)), Thickness);
            context.DrawEllipse(null, track, center, Radius, Radius);

            if (remaining is not { } value)
                return;

            var fraction = Math.Clamp(value / 100, 0, 1);
            if (fraction <= 0.001)
                return;

            var pen = new Pen(new SolidColorBrush(ColorFor(UsageLevels.For(value))), Thickness, lineCap: PenLineCap.Round);

            if (fraction >= 0.999)
            {
                context.DrawEllipse(null, pen, center, Radius, Radius);
                return;
            }

            // Start oben (12 Uhr), im Uhrzeigersinn; die Länge ist der verbleibende Anteil.
            var angle = fraction * 2 * Math.PI;
            var start = new Point(center.X, center.Y - Radius);
            var end = new Point(center.X + Radius * Math.Sin(angle), center.Y - Radius * Math.Cos(angle));

            var geometry = new StreamGeometry();
            using (var g = geometry.Open())
            {
                g.BeginFigure(start, false);
                g.ArcTo(end, new Size(Radius, Radius), 0, angle > Math.PI, SweepDirection.Clockwise);
                g.EndFigure(false);
            }

            context.DrawGeometry(null, pen, geometry);
        }

        private static Color ColorFor(UsageLevel level) => level switch
        {
            UsageLevel.Ok => Color.FromRgb(0x34, 0xC7, 0x59),
            UsageLevel.Low => Color.FromRgb(0xFF, 0x9F, 0x0A),
            UsageLevel.Critical => Color.FromRgb(0xFF, 0x45, 0x3A),
            _ => Color.FromRgb(0x8E, 0x8E, 0x93),
        };
    }
}
