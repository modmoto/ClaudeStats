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
    private MenuItems? _menu;
    private UsageSnapshot? _snapshot;
    private FetchResult? _lastResult;
    private DateTimeOffset _nextFetch = DateTimeOffset.MinValue;
    private bool _fetching;

    // The native macOS menu cannot be swapped for a new NativeMenu on every update
    // ("The menu being updated does not match"), so the menu is built once and only its items change.
    private sealed class MenuItems
    {
        public required NativeMenuItem FiveHour { get; init; }
        public required NativeMenuItem Week { get; init; }
        public required NativeMenuItem Status { get; init; }
        public required NativeMenuItem Age { get; init; }
        public NativeMenuItem? Autostart { get; set; }
    }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            Log.Info($"Starting ClaudeStats on {Environment.OSVersion}, exe {Environment.ProcessPath}");
            CredentialStore.Log = Log.Info;

            // No main window: the app lives only in the menu bar / tray.
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            try
            {
                // One square icon with two bars: the macOS menu bar squeezes and clips wide images.
                _tray = new TrayIcon
                {
                    Icon = RingIconRenderer.Render(null, null),
                    ToolTipText = "ClaudeStats",
                    Menu = BuildMenu(desktop, out var menu),
                    IsVisible = true,
                };
                _menu = menu;

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
                // Not logged in / expired: check again soon, the user may be fixing it right now.
                FetchStatus.NotLoggedIn or FetchStatus.TokenExpired or FetchStatus.Unauthorized => TimeSpan.FromSeconds(30),
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
        if (_tray is null || _menu is null)
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

            UpdateMenu(_menu, now);
        }
        catch (Exception e)
        {
            Log.Error("Redraw failed", e);
        }
    }

    private void UpdateMenu(MenuItems menu, DateTimeOffset now)
    {
        menu.FiveHour.Header = UsageFormatter.Line("5-Stunden-Limit", _snapshot?.FiveHour, now);
        menu.Week.Header = UsageFormatter.Line("Wochenlimit", _snapshot?.SevenDay, now);

        var showStatus = _lastResult is { Status: not FetchStatus.Ok };
        menu.Status.IsVisible = showStatus;
        menu.Status.Header = showStatus ? StatusText() : " ";

        var showAge = _snapshot is not null;
        menu.Age.IsVisible = showAge;
        menu.Age.Header = showAge ? $"Stand: {UsageFormatter.Age(now - _snapshot!.UpdatedAt)}" : " ";

        if (menu.Autostart is not null)
            menu.Autostart.IsChecked = Autostart.IsEnabled;
    }

    private string StatusText() => _lastResult?.Status switch
    {
        null => "Lade …",
        FetchStatus.Ok => "OK",
        FetchStatus.NotLoggedIn => "Claude-Code-Login nicht gefunden – siehe Log",
        FetchStatus.TokenExpired => "Login abgelaufen – Claude Code kurz öffnen, dann aktualisieren",
        FetchStatus.Unauthorized => "Zugriff abgelehnt – in Claude Code neu einloggen (/login)",
        FetchStatus.RateLimited => "Zu viele Anfragen – nächster Versuch später",
        _ => "Abruf fehlgeschlagen – siehe Log",
    };

    private static string FormatOrDash(double? value) =>
        value is { } v ? UsageFormatter.Percent(v) : "–";

    private NativeMenu BuildMenu(IClassicDesktopStyleApplicationLifetime desktop, out MenuItems items)
    {
        var menu = new NativeMenu();

        items = new MenuItems
        {
            FiveHour = Info("5-Stunden-Limit: …", enabled: true),
            Week = Info("Wochenlimit: …", enabled: true),
            Status = Info(" "),
            Age = Info(" "),
        };

        menu.Add(items.FiveHour);
        menu.Add(items.Week);
        menu.Add(new NativeMenuItemSeparator());
        menu.Add(items.Status);
        menu.Add(items.Age);
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
            items.Autostart = autostart;
        }

        var quit = new NativeMenuItem("Beenden");
        quit.Click += (_, _) => desktop.Shutdown();
        menu.Add(quit);

        return menu;
    }

    // Disabled items are drawn grey by macOS. The two limit lines are kept enabled (they do nothing
    // when clicked) so they show in normal text color; status and age stay grey as secondary info.
    private static NativeMenuItem Info(string text, bool enabled = false)
    {
        var item = new NativeMenuItem(text) { IsEnabled = enabled };
        // macOS also draws items grey when they have no action at all, so give enabled ones a no-op.
        if (enabled)
            item.Click += (_, _) => { };
        return item;
    }
}
