using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using ClaudeStats.Core;

namespace ClaudeStats;

/// <summary>Draws the tray icon: 5-hour ring on the left, weekly ring on the right.</summary>
public static class RingIconRenderer
{
    private const double Width = 40;
    private const double Height = 22;

    /// <param name="fiveHourRemaining">Remaining percentage (0–100), or null if unknown.</param>
    /// <param name="weekRemaining">Remaining percentage (0–100), or null if unknown.</param>
    public static WindowIcon Render(double? fiveHourRemaining, double? weekRemaining)
    {
        // Encode to PNG and build the icon from that stream. Handing a RenderTargetBitmap directly
        // to the native tray is less reliable than the regular "icon from image file" path.
        var stream = new MemoryStream(RenderPng(fiveHourRemaining, weekRemaining));
        return new WindowIcon(stream);
    }

    /// <summary>The icon as PNG bytes (80×44 px, i.e. 2x for Retina).</summary>
    public static byte[] RenderPng(double? fiveHourRemaining, double? weekRemaining)
    {
        var visual = new RingsVisual(fiveHourRemaining, weekRemaining) { Width = Width, Height = Height };
        visual.Measure(new Size(Width, Height));
        visual.Arrange(new Rect(0, 0, Width, Height));

        // 2x for Retina displays; the menu bar scales it down to its height.
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)Width * 2, (int)Height * 2), new Vector(192, 192));
        bitmap.Render(visual);

        using var png = new MemoryStream();
        bitmap.Save(png);
        return png.ToArray();
    }

    private sealed class RingsVisual(double? five, double? week) : Control
    {
        private const double Radius = 7.5;
        private const double Thickness = 3;

        public override void Render(DrawingContext context)
        {
            DrawRing(context, new Point(10, 11), five);
            DrawRing(context, new Point(30, 11), week);
        }

        private static void DrawRing(DrawingContext context, Point center, double? remaining)
        {
            // Mid grey track: visible on both light and dark menu bars.
            var track = new Pen(new SolidColorBrush(Color.FromArgb(110, 128, 128, 128)), Thickness);
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

            // Start at the top (12 o'clock), clockwise; the arc length is the remaining share.
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
