using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using ClaudeStats.Core;

namespace ClaudeStats;

public class App : Application
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(10);

    private TrayIcon? _tray;
    private DispatcherTimer? _timer;
    private UsageSnapshot? _snapshot;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // No main window: the app lives only in the menu bar / tray.
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            _tray = new TrayIcon { IsVisible = true };
            TrayIcon.SetIcons(this, new TrayIcons { _tray });

            Refresh();

            _timer = new DispatcherTimer { Interval = RefreshInterval };
            _timer.Tick += (_, _) => Refresh();
            _timer.Start();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void Refresh()
    {
        if (_tray is null)
            return;

        // If reading fails (file is being replaced right now), keep the last known state.
        _snapshot = UsageFile.Load() ?? _snapshot;

        var now = DateTimeOffset.Now;
        var five = _snapshot?.FiveHour?.RemainingPercentage(now);
        var week = _snapshot?.SevenDay?.RemainingPercentage(now);

        _tray.Icon = RingIconRenderer.Render(five, week);
        _tray.ToolTipText = five is null && week is null
            ? "ClaudeStats: keine Daten"
            : $"5 h: {FormatOrDash(five)} übrig · Woche: {FormatOrDash(week)} übrig";
        _tray.Menu = BuildMenu(now);
    }

    private static string FormatOrDash(double? value) =>
        value is { } v ? UsageFormatter.Percent(v) : "–";

    private NativeMenu BuildMenu(DateTimeOffset now)
    {
        var menu = new NativeMenu();

        menu.Add(Info(UsageFormatter.Line("5-Stunden-Limit", _snapshot?.FiveHour, now)));
        menu.Add(Info(UsageFormatter.Line("Wochenlimit", _snapshot?.SevenDay, now)));
        menu.Add(new NativeMenuItemSeparator());

        menu.Add(Info(_snapshot is null
            ? "Noch keine Daten – Statusline einrichten (siehe README)"
            : $"Stand: {UsageFormatter.Age(now - _snapshot.UpdatedAt)}"));
        menu.Add(new NativeMenuItemSeparator());

        var refresh = new NativeMenuItem("Aktualisieren");
        refresh.Click += (_, _) => Refresh();
        menu.Add(refresh);

        var quit = new NativeMenuItem("Beenden");
        quit.Click += (_, _) =>
            (ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
        menu.Add(quit);

        return menu;
    }

    private static NativeMenuItem Info(string text) => new(text) { IsEnabled = false };
}
