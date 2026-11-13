using System.Diagnostics;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using SpartanX.App.Animation;
using SpartanX.App.Services;
using SpartanX.App.Settings;
using SpartanX.App.Shell;
using SpartanX.App.Welcome;
using SpartanX.Core;
using SpartanX.Core.Abstractions;
using SpartanX.Core.Dock;
using SpartanX.Core.Island;
using SpartanX.Core.Lifecycle;
using SpartanX.Core.Localization;
using SpartanX.Core.MenuBar;
using SpartanX.Core.Settings;
using SpartanX.Core.Updates;
using SpartanX.Core.Welcome;
using SpartanX.Core.Weather;

namespace SpartanX.App;

public partial class App : Application
{
    private Mutex? _instanceMutex;
    private EventWaitHandle? _activateEvent;
    private EventWaitHandle? _quitEvent;
    private ServiceProvider? _services;
    private ShellController? _shell;
    private WelcomeWindow? _welcome;
    private SettingsWindow? _settingsWindow;
    private readonly CancellationTokenSource _lifetime = new();

    public App()
    {
        InitializeComponent();
        UnhandledException += (_, e) =>
        {
            Log(e.Exception);
            // Never leave the user without a taskbar.
            _shell?.EmergencyRestore();
        };
        AppDomain.CurrentDomain.ProcessExit += (_, _) => _shell?.EmergencyRestore();
    }

    public static new App Current => (App)Application.Current;

    public static DispatcherQueue? MainQueue { get; private set; }

    public static string DataDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppInfo.Name);

    public IServiceProvider Services => _services ?? throw new InvalidOperationException("Not started");

    /// <summary>Owner window for file pickers opened from Settings.</summary>
    internal nint SettingsWindowHandle => _settingsWindow is null ? 0 : Interop.WindowHelper.GetHwnd(_settingsWindow);

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // `SpartanX.exe --quit` / `--uninstall [--purge] [--silent]`: headless maintenance, no UI.
        var options = CommandLineOptions.Parse(Environment.GetCommandLineArgs().Skip(1));
        if (options.Command != LaunchCommand.Run)
        {
            Environment.Exit(MaintenanceRunner.Run(options));
            return;
        }

        if (!AcquireSingleInstance())
        {
            Environment.Exit(0);
            return;
        }

        MainQueue = DispatcherQueue.GetForCurrentThread();
        _services = ConfigureServices(MainQueue);

        var settings = Services.GetRequiredService<ISettingsService>();
        Localizer.SetLanguage(settings.Current.General.Language);
        Motion.Settings = settings.Current.Animations;
        Services.GetRequiredService<ThemeService>().UpdateAccentResources();

        if (settings.Current.General.FirstRunCompleted)
        {
            StartShell();
        }
        else
        {
            ShowWelcome();
        }
    }

    private static ServiceProvider ConfigureServices(DispatcherQueue queue)
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"{AppInfo.Name}/{AppInfo.Version}");

        var services = new ServiceCollection();
        services.AddSingleton(http);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(queue);
        services.AddSingleton<IUiDispatcher>(new UiDispatcher(queue));
        services.AddSingleton<SettingsService>(_ => new SettingsService(DataDirectory));
        services.AddSingleton<ISettingsService>(sp => sp.GetRequiredService<SettingsService>());

        services.AddSingleton<SystemStatusService>();
        services.AddSingleton<ISystemStatusService>(sp => sp.GetRequiredService<SystemStatusService>());
        services.AddSingleton<IMediaService, MediaService>();
        services.AddSingleton<INotificationService, NotificationService>();
        services.AddSingleton<IWindowTracker, WindowTracker>();
        services.AddSingleton<IAppLauncher, AppLauncher>();
        services.AddSingleton<IIconProvider, IconProvider>();
        services.AddSingleton<IStartupService, StartupService>();
        services.AddSingleton<IWeatherService, OpenMeteoWeatherService>();
        services.AddSingleton<IUpdateClient>(sp => new GitHubReleaseClient(sp.GetRequiredService<HttpClient>(), AppInfo.RepositoryOwner, AppInfo.RepositoryName));
        services.AddSingleton<UpdateService>();
        services.AddSingleton<ThemeService>();
        services.AddSingleton<HotkeyService>();

        services.AddSingleton<DockViewModel>();
        services.AddSingleton<IslandViewModel>();
        services.AddSingleton<MenuBarViewModel>();
        services.AddTransient<WelcomeViewModel>();
        services.AddSingleton<ShellController>();
        return services.BuildServiceProvider();
    }

    private void ShowWelcome()
    {
        _welcome = new WelcomeWindow(Services.GetRequiredService<WelcomeViewModel>(), Services.GetRequiredService<ThemeService>(), Services.GetRequiredService<IStartupService>());
        _welcome.Completed += (_, _) =>
        {
            _welcome = null;
            StartShell();
        };
        _welcome.Activate();
    }

    private void StartShell()
    {
        _shell = Services.GetRequiredService<ShellController>();
        _shell.Start();
        _ = Services.GetRequiredService<UpdateService>().RunBackgroundChecksAsync(_lifetime.Token)
            .ContinueWith(_ => { }, TaskScheduler.Default);
    }

    /// <summary>Opens (or focuses) the System Settings-style window.</summary>
    public void OpenSettings(string? page = null)
    {
        if (_settingsWindow is null)
        {
            _settingsWindow = new SettingsWindow(Services);
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        }

        _settingsWindow.Navigate(page);
        _settingsWindow.Activate();
    }

    /// <summary>Re-runs the Setup Assistant from Settings → General.</summary>
    public void RerunWelcome()
    {
        if (_welcome is not null)
        {
            _welcome.Activate();
            return;
        }

        _welcome = new WelcomeWindow(Services.GetRequiredService<WelcomeViewModel>(), Services.GetRequiredService<ThemeService>(), Services.GetRequiredService<IStartupService>());
        _welcome.Completed += (_, _) => _welcome = null;
        _welcome.Activate();
    }

    public void Restart()
    {
        Process.Start(new ProcessStartInfo(Environment.ProcessPath!, "--restarted") { UseShellExecute = false })?.Dispose();
        Quit();
    }

    public void Quit()
    {
        _lifetime.Cancel();
        _shell?.Dispose();
        _shell = null;
        _services?.GetService<SettingsService>()?.Dispose();
        ReleaseSingleInstance();
        Exit();

        // Guarantee the process really ends (so its folder can be deleted) even if a
        // foreground thread or a WinRT callback would otherwise keep it alive.
        new Thread(() =>
        {
            Thread.Sleep(3000);
            Environment.Exit(0);
        })
        { IsBackground = true }.Start();
    }

    /// <summary>
    /// Settings → General → "Prepare to uninstall": hands over to a separate
    /// <c>--uninstall</c> process that closes this one and cleans up.
    /// </summary>
    public void PrepareUninstall(bool purgeSettings)
    {
        var args = purgeSettings ? "--uninstall --purge" : "--uninstall";
        Process.Start(new ProcessStartInfo(Environment.ProcessPath!, args) { UseShellExecute = false })?.Dispose();
    }

    private bool AcquireSingleInstance()
    {
        _instanceMutex = new Mutex(true, InstanceControl.MutexName, out var created);
        if (!created)
        {
            // Another instance is running: ask it to show Settings, then leave.
            try
            {
                using var existing = EventWaitHandle.OpenExisting(InstanceControl.ActivateEventName);
                existing.Set();
            }
            catch (WaitHandleCannotBeOpenedException)
            {
            }

            return false;
        }

        _activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, InstanceControl.ActivateEventName);
        ThreadPool.RegisterWaitForSingleObject(_activateEvent, (_, _) => MainQueue?.TryEnqueue(() => OpenSettings()), null, Timeout.Infinite, false);

        // Lets `SpartanX.exe --quit` / `--uninstall` close this instance cleanly.
        _quitEvent = new EventWaitHandle(false, EventResetMode.AutoReset, InstanceControl.QuitEventName);
        ThreadPool.RegisterWaitForSingleObject(_quitEvent, (_, _) => MainQueue?.TryEnqueue(Quit), null, Timeout.Infinite, true);
        return true;
    }

    private void ReleaseSingleInstance()
    {
        try
        {
            _instanceMutex?.ReleaseMutex();
        }
        catch (ApplicationException)
        {
        }

        _instanceMutex?.Dispose();
        _activateEvent?.Dispose();
        _quitEvent?.Dispose();
    }

    internal static void Log(Exception exception)
    {
        try
        {
            Directory.CreateDirectory(DataDirectory);
            File.AppendAllText(Path.Combine(DataDirectory, "error.log"), $"[{DateTimeOffset.Now:O}] {exception}\n\n");
        }
        catch (IOException)
        {
        }
    }
}
