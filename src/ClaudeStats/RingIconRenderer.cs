using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using ClaudeStats.Core;

namespace ClaudeStats;

/// <summary>
/// Draws one square tray icon: a ring that counts down, with the remaining percentage in its center.
/// The app shows two of these side by side (5-hour and weekly), because a single wide tray image
/// gets squeezed and clipped by the macOS menu bar.
/// </summary>
public static class RingIconRenderer
{
    private const double Size = 22;

    /// <param name="remaining">Remaining percentage (0–100), or null if unknown.</param>
    public static WindowIcon Render(double? remaining)
    {
        // Encode to PNG and build the icon from that stream. Handing a RenderTargetBitmap directly
        // to the native tray is less reliable than the regular "icon from image file" path.
        var stream = new MemoryStream(RenderPng(remaining));
        return new WindowIcon(stream);
    }

    /// <summary>The icon as PNG bytes (44×44 px, i.e. 2x for Retina).</summary>
    public static byte[] RenderPng(double? remaining)
    {
        var visual = new RingVisual(remaining) { Width = Size, Height = Size };
        visual.Measure(new Size(Size, Size));
        visual.Arrange(new Rect(0, 0, Size, Size));

        // 2x for Retina displays; the menu bar scales it down to its height.
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)Size * 2, (int)Size * 2), new Vector(192, 192));
        bitmap.Render(visual);

        using var png = new MemoryStream();
        bitmap.Save(png);
        return png.ToArray();
    }

    private sealed class RingVisual(double? remaining) : Control
    {
        private const double Radius = 9;
        private const double Thickness = 2.6;

        private static readonly Typeface Font = new(
            new FontFamily("Helvetica Neue, Helvetica, Arial, sans-serif"), FontStyle.Normal, FontWeight.Bold);

        public override void Render(DrawingContext context)
        {
            var center = new Point(Size / 2, Size / 2);

            // Mid grey track: visible on both light and dark menu bars.
            var track = new Pen(new SolidColorBrush(Color.FromArgb(120, 140, 140, 140)), Thickness);
            context.DrawEllipse(null, track, center, Radius, Radius);

            var color = ColorFor(UsageLevels.For(remaining));
            DrawNumber(context, center, color);

            if (remaining is not { } value)
                return;

            var fraction = Math.Clamp(value / 100, 0, 1);
            if (fraction <= 0.001)
                return;

            var pen = new Pen(new SolidColorBrush(color), Thickness, lineCap: PenLineCap.Round);

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

        private void DrawNumber(DrawingContext context, Point center, Color color)
        {
            var text = remaining is { } v ? Math.Round(v).ToString("0", CultureInfo.InvariantCulture) : "–";
            var size = text.Length >= 3 ? 7.0 : 9.5;

            var formatted = new FormattedText(
                text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Font, size, new SolidColorBrush(color));

            context.DrawText(formatted, new Point(center.X - formatted.Width / 2, center.Y - formatted.Height / 2));
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
