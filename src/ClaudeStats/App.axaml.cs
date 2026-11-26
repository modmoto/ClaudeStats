using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using ClaudeStats.Core;

namespace ClaudeStats;

public class App : Application
{
    // The endpoint is rate limited; every 2 minutes is plenty for a usage indicator.
    private static readonly TimeSpan FetchInterval = TimeSpan.FromMinutes(2);
    // Redraw more often so countdowns and "reset passed" stay current between fetches.
    private static readonly TimeSpan RedrawInterval = TimeSpan.FromSeconds(30);

    private readonly UsageClient _client = new(new HttpClient { Timeout = TimeSpan.FromSeconds(15) });

    private TrayIcon? _tray;
    private UsageSnapshot? _snapshot;
    private FetchResult? _lastResult;
    private DateTimeOffset _nextFetch = DateTimeOffset.MinValue;
    private bool _fetching;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            Log.Info($"Starting ClaudeStats on {Environment.OSVersion}, exe {Environment.ProcessPath}");

            // No main window: the app lives only in the menu bar / tray.
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            try
            {
                _tray = new TrayIcon
                {
                    Icon = RingIconRenderer.Render(null, null),
                    ToolTipText = "ClaudeStats",
                    IsVisible = true,
                };
                TrayIcon.SetIcons(this, new TrayIcons { _tray });
                Log.Info("Tray icon created");
            }
            catch (Exception e)
            {
                Log.Error("Could not create tray icon", e);
            }

            Redraw();

            var timer = new DispatcherTimer { Interval = RedrawInterval };
            timer.Tick += (_, _) => Tick();
            timer.Start();
            Tick();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void Tick()
    {
        if (DateTimeOffset.UtcNow >= _nextFetch)
            _ = FetchAsync();
        else
            Redraw();
    }

    private async Task FetchAsync()
    {
        if (_fetching)
            return;
        _fetching = true;

        try
        {
            // Reading the Keychain can block on the macOS permission dialog, so stay off the UI thread.
            var result = await Task.Run(() => _client.FetchAsync());
            _lastResult = result;

            if (result.Status == FetchStatus.Ok)
                _snapshot = result.Snapshot;
            else
                Log.Info($"Fetch: {result.Status} {result.Detail}");

            var wait = result.Status switch
            {
                FetchStatus.RateLimited => result.RetryAfter is { } r && r > FetchInterval ? r : FetchInterval * 2,
                _ => FetchInterval,
            };
            _nextFetch = DateTimeOffset.UtcNow + wait;
        }
        catch (Exception e)
        {
            Log.Error("Fetch crashed", e);
            _nextFetch = DateTimeOffset.UtcNow + FetchInterval;
        }
        finally
        {
            _fetching = false;
            Redraw();
        }
    }

    private void Redraw()
    {
        if (_tray is null)
            return;

        try
        {
            var now = DateTimeOffset.UtcNow;
            var five = _snapshot?.FiveHour?.RemainingPercentage(now);
            var week = _snapshot?.SevenDay?.RemainingPercentage(now);

            _tray.Icon = RingIconRenderer.Render(five, week);
            _tray.ToolTipText = five is null && week is null
                ? "ClaudeStats: " + StatusText()
                : $"5 h: {FormatOrDash(five)} übrig · Woche: {FormatOrDash(week)} übrig";
            _tray.Menu = BuildMenu(now);
        }
        catch (Exception e)
        {
            Log.Error("Redraw failed", e);
        }
    }

    private string StatusText() => _lastResult?.Status switch
    {
        null => "Lade …",
        FetchStatus.Ok => "OK",
        FetchStatus.NotLoggedIn => "Claude Code nicht angemeldet – im Terminal `claude` starten und einloggen",
        FetchStatus.TokenExpired => "Login abgelaufen – Claude Code kurz öffnen, dann aktualisieren",
        FetchStatus.Unauthorized => "Zugriff abgelehnt – in Claude Code neu einloggen (/login)",
        FetchStatus.RateLimited => "Zu viele Anfragen – nächster Versuch später",
        _ => "Abruf fehlgeschlagen – siehe Log",
    };

    private static string FormatOrDash(double? value) =>
        value is { } v ? UsageFormatter.Percent(v) : "–";

    private NativeMenu BuildMenu(DateTimeOffset now)
    {
        var menu = new NativeMenu();

        menu.Add(Info(UsageFormatter.Line("5-Stunden-Limit", _snapshot?.FiveHour, now)));
        menu.Add(Info(UsageFormatter.Line("Wochenlimit", _snapshot?.SevenDay, now)));
        menu.Add(new NativeMenuItemSeparator());

        if (_lastResult is { Status: not FetchStatus.Ok })
            menu.Add(Info(StatusText()));
        if (_snapshot is not null)
            menu.Add(Info($"Stand: {UsageFormatter.Age(now - _snapshot.UpdatedAt)}"));
        menu.Add(new NativeMenuItemSeparator());

        var refresh = new NativeMenuItem("Aktualisieren");
        refresh.Click += (_, _) => { _nextFetch = DateTimeOffset.MinValue; _ = FetchAsync(); };
        menu.Add(refresh);

        if (Autostart.IsSupported)
        {
            var autostart = new NativeMenuItem("Beim Anmelden starten")
            {
                ToggleType = NativeMenuItemToggleType.CheckBox,
                IsChecked = Autostart.IsEnabled,
            };
            autostart.Click += (_, _) =>
            {
                try { Autostart.SetEnabled(!Autostart.IsEnabled); }
                catch (Exception e) { Log.Error("Autostart toggle failed", e); }
                Redraw();
            };
            menu.Add(autostart);
        }

        var log = new NativeMenuItem("Log öffnen");
        log.Click += (_, _) => OpenLog();
        menu.Add(log);

        var quit = new NativeMenuItem("Beenden");
        quit.Click += (_, _) =>
            (ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
        menu.Add(quit);

        return menu;
    }

    private static void OpenLog()
    {
        try
        {
            Log.Info("Log opened");
            var opener = OperatingSystem.IsMacOS() ? "open" : OperatingSystem.IsWindows() ? "notepad" : "xdg-open";
            System.Diagnostics.Process.Start(opener, Log.FilePath);
        }
        catch (Exception e)
        {
            Log.Error("Could not open log", e);
        }
    }

    private static NativeMenuItem Info(string text) => new(text) { IsEnabled = false };
}
