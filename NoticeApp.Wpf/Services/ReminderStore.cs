using System.Text.Json;
using System.IO;
using NoticeApp.Wpf.Models;

namespace NoticeApp.Wpf.Services;

public sealed class ReminderStore
{
    private readonly string _storageFilePath;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
    };

    public ReminderStore(string? storageFilePath = null)
    {
        _storageFilePath = storageFilePath ?? BuildDefaultStoragePath();
    }

    public string StorageFilePath => _storageFilePath;

    public IReadOnlyList<Reminder> Load()
    {
        try
        {
            if (!File.Exists(_storageFilePath))
            {
                return [];
            }

            var json = File.ReadAllText(_storageFilePath);
            var reminders = JsonSerializer.Deserialize<List<Reminder>>(json, _jsonOptions);
            return reminders ?? [];
        }
        catch
        {
            return [];
        }
    }

    public void Save(IEnumerable<Reminder> reminders)
    {
        var directory = Path.GetDirectoryName(_storageFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var snapshot = reminders.ToList();
        var json = JsonSerializer.Serialize(snapshot, _jsonOptions);
        File.WriteAllText(_storageFilePath, json);
    }

    private static string BuildDefaultStoragePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var directory = Path.Combine(appData, "NoticeApp");
        return Path.Combine(directory, "reminders.json");
    }
}
