using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using ClaudeStats.Core;

namespace ClaudeStats;

/// <summary>
/// Draws the tray icon: two horizontal bars in one square field, no numbers.
/// Top bar = 5-hour limit, bottom bar = weekly limit. The bars count down: full = plenty left.
/// The image is deliberately square, because the macOS menu bar clips wide tray images.
/// (The class name is historical; it used to draw rings.)
/// </summary>
public static class RingIconRenderer
{
    private const double Size = 22;

    /// <param name="fiveHourRemaining">Remaining percentage (0–100), or null if unknown.</param>
    /// <param name="weekRemaining">Remaining percentage (0–100), or null if unknown.</param>
    public static WindowIcon Render(double? fiveHourRemaining, double? weekRemaining)
    {
        // Encode to PNG and build the icon from that stream. Handing a RenderTargetBitmap directly
        // to the native tray is less reliable than the regular "icon from image file" path.
        var stream = new MemoryStream(RenderPng(fiveHourRemaining, weekRemaining));
        return new WindowIcon(stream);
    }

    /// <summary>The icon as PNG bytes (44×44 px, i.e. 2x for Retina).</summary>
    public static byte[] RenderPng(double? fiveHourRemaining, double? weekRemaining)
    {
        var visual = new BarsVisual(fiveHourRemaining, weekRemaining) { Width = Size, Height = Size };
        visual.Measure(new Size(Size, Size));
        visual.Arrange(new Rect(0, 0, Size, Size));

        // 2x for Retina displays; the menu bar scales it down to its height.
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)Size * 2, (int)Size * 2), new Vector(192, 192));
        bitmap.Render(visual);

        using var png = new MemoryStream();
        bitmap.Save(png);
        return png.ToArray();
    }

    private sealed class BarsVisual(double? five, double? week) : Control
    {
        private const double BarLeft = 1;
        private const double BarWidth = 20;
        private const double BarHeight = 7;
        private const double Gap = 3;
        private const double Radius = BarHeight / 2;

        public override void Render(DrawingContext context)
        {
            var top = (Size - (2 * BarHeight + Gap)) / 2;
            DrawBar(context, top, five);
            DrawBar(context, top + BarHeight + Gap, week);
        }

        private static void DrawBar(DrawingContext context, double y, double? remaining)
        {
            // Mid grey track: visible on both light and dark menu bars.
            var track = new SolidColorBrush(Color.FromArgb(120, 140, 140, 140));
            context.DrawRectangle(track, null, new RoundedRect(new Rect(BarLeft, y, BarWidth, BarHeight), Radius, Radius));

            if (remaining is not { } value)
                return;

            var fraction = Math.Clamp(value / 100, 0, 1);
            if (fraction <= 0.001)
                return;

            // At least 2 px so a nearly empty bar is still visible.
            var width = Math.Max(2, BarWidth * fraction);
            var radius = Math.Min(Radius, width / 2);
            var fill = new SolidColorBrush(ColorFor(UsageLevels.For(value)));
            context.DrawRectangle(fill, null, new RoundedRect(new Rect(BarLeft, y, width, BarHeight), radius, radius));
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
