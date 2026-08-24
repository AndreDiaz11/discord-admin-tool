using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace DiscordAdminTool.Services;

public static class ActivityStore
{
    private static readonly string FolderPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "discord-admin-tool-app");
    private static readonly string FilePath = Path.Combine(FolderPath, "activity-tracker.json");
    private static Dictionary<ulong, DateTimeOffset>? _lastSeen;
    private static readonly object Lock = new();

    private static Dictionary<ulong, DateTimeOffset> Load()
    {
        if (_lastSeen != null) return _lastSeen;
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                _lastSeen = JsonSerializer.Deserialize<Dictionary<ulong, DateTimeOffset>>(json) ?? new();
                return _lastSeen;
            }
        }
        catch { }
        _lastSeen = new Dictionary<ulong, DateTimeOffset>();
        return _lastSeen;
    }

    private static void Persist()
    {
        try
        {
            Directory.CreateDirectory(FolderPath);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(_lastSeen));
        }
        catch { }
    }

    public static void MarkSeen(ulong userId)
    {
        lock (Lock)
        {
            Load()[userId] = DateTimeOffset.UtcNow;
            Persist();
        }
    }

    public static DateTimeOffset? GetLastSeen(ulong userId)
    {
        lock (Lock)
        {
            return Load().TryGetValue(userId, out var v) ? v : null;
        }
    }
}
