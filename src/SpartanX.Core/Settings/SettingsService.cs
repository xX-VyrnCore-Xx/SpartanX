using System.Collections.Specialized;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SpartanX.Core.Settings;

public interface ISettingsService
{
    AppSettings Current { get; }

    /// <summary>Raised (on the thread that made the change) whenever any preference changes.</summary>
    event EventHandler<SettingChangedEventArgs>? Changed;

    Task SaveAsync(CancellationToken cancellationToken = default);

    /// <summary>Restores every section to its defaults, keeping <see cref="GeneralSettings.FirstRunCompleted"/>.</summary>
    void ResetToDefaults();
}

public sealed class SettingChangedEventArgs(object section, string? propertyName) : EventArgs
{
    public object Section { get; } = section;
    public string? PropertyName { get; } = propertyName;
}

/// <summary>
/// Loads settings from a JSON file and writes them back, debounced, whenever something changes.
/// Writes are atomic (temp file + rename) so a crash never leaves a half-written file.
/// </summary>
public sealed class SettingsService : ISettingsService, IDisposable
{
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly string _filePath;
    private readonly TimeSpan _debounce;
    private readonly Timer _saveTimer;
    private readonly SemaphoreSlim _ioLock = new(1, 1);
    private bool _disposed;

    public SettingsService(string directory, TimeSpan? debounce = null)
    {
        Directory.CreateDirectory(directory);
        _filePath = Path.Combine(directory, "settings.json");
        _debounce = debounce ?? TimeSpan.FromMilliseconds(400);
        _saveTimer = new Timer(_ => _ = SaveAsync(), null, Timeout.Infinite, Timeout.Infinite);
        Current = Load(_filePath);
        Attach(Current);
    }

    public AppSettings Current { get; private set; }

    public string FilePath => _filePath;

    public event EventHandler<SettingChangedEventArgs>? Changed;

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            return;
        }

        await _ioLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var json = JsonSerializer.Serialize(Current, JsonOptions);
            var temp = _filePath + ".tmp";
            await File.WriteAllTextAsync(temp, json, cancellationToken).ConfigureAwait(false);
            File.Move(temp, _filePath, overwrite: true);
        }
        finally
        {
            _ioLock.Release();
        }
    }

    public void ResetToDefaults()
    {
        var firstRun = Current.General.FirstRunCompleted;
        Detach(Current);
        Current = new AppSettings();
        Current.General.FirstRunCompleted = firstRun;
        Attach(Current);
        Changed?.Invoke(this, new SettingChangedEventArgs(Current, null));
        ScheduleSave();
    }

    internal static AppSettings Load(string path)
    {
        if (!File.Exists(path))
        {
            return new AppSettings();
        }

        try
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), JsonOptions) ?? new AppSettings();
            return Migrate(settings);
        }
        catch (JsonException)
        {
            // Keep the broken file for inspection and start fresh rather than crash at login.
            File.Copy(path, path + ".corrupt", overwrite: true);
            return new AppSettings();
        }
    }

    private static AppSettings Migrate(AppSettings settings)
    {
        // Sections missing from an older file deserialize as null-free defaults thanks to
        // initialisers; future schema migrations go here.
        settings.SchemaVersion = AppSettings.CurrentSchemaVersion;
        return settings;
    }

    private void Attach(AppSettings settings)
    {
        foreach (var section in settings.Sections)
        {
            section.PropertyChanged += OnSectionChanged;
        }

        settings.Dock.PinnedApps.CollectionChanged += OnCollectionChanged;
        settings.Dock.Stacks.CollectionChanged += OnCollectionChanged;
    }

    private void Detach(AppSettings settings)
    {
        foreach (var section in settings.Sections)
        {
            section.PropertyChanged -= OnSectionChanged;
        }

        settings.Dock.PinnedApps.CollectionChanged -= OnCollectionChanged;
        settings.Dock.Stacks.CollectionChanged -= OnCollectionChanged;
    }

    private void OnSectionChanged(object? sender, PropertyChangedEventArgs e)
    {
        Changed?.Invoke(this, new SettingChangedEventArgs(sender!, e.PropertyName));
        ScheduleSave();
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        var name = ReferenceEquals(sender, Current.Dock.Stacks) ? nameof(DockSettings.Stacks) : nameof(DockSettings.PinnedApps);
        Changed?.Invoke(this, new SettingChangedEventArgs(Current.Dock, name));
        ScheduleSave();
    }

    private void ScheduleSave()
    {
        if (!_disposed)
        {
            _saveTimer.Change(_debounce, Timeout.InfiniteTimeSpan);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _saveTimer.Dispose();
        // Flush pending changes synchronously on shutdown.
        SaveAsync().GetAwaiter().GetResult();
        _disposed = true;
        Detach(Current);
        _ioLock.Dispose();
    }
}
