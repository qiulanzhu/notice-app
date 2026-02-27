using System.IO;
using System.Text.Json;
using NoticeApp.Wpf.Models;

namespace NoticeApp.Wpf.Services;

public sealed class AppSettingsStore
{
    private readonly string _storageFilePath;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
    };

    public AppSettingsStore(string? storageFilePath = null)
    {
        _storageFilePath = storageFilePath ?? BuildDefaultStoragePath();
    }

    public string StorageFilePath => _storageFilePath;

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_storageFilePath))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(_storageFilePath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions) ?? new AppSettings();
            settings.NotificationPlacement = Enum.IsDefined(settings.NotificationPlacement)
                ? settings.NotificationPlacement
                : NotificationPlacement.BottomRight;

            return settings;
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        var directory = Path.GetDirectoryName(_storageFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        settings.NotificationPlacement = Enum.IsDefined(settings.NotificationPlacement)
            ? settings.NotificationPlacement
            : NotificationPlacement.BottomRight;

        var json = JsonSerializer.Serialize(settings, _jsonOptions);
        File.WriteAllText(_storageFilePath, json);
    }

    private static string BuildDefaultStoragePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var directory = Path.Combine(appData, "NoticeApp");
        return Path.Combine(directory, "settings.json");
    }
}
