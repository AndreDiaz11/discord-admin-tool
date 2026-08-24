using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace DiscordAdminTool.Services;

public static class PresenceStore
{
    private static readonly string FolderPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "discord-admin-tool-app");
    private static readonly string FilePath = Path.Combine(FolderPath, "presence-tracker.json");

    private class Data
    {
        public Dictionary<ulong, DateTimeOffset> LastOnline { get; set; } = new();
        public Dictionary<ulong, DateTimeOffset> LastVoice { get; set; } = new();
    }

    private static Data? _data;
    private static readonly object Lock = new();

    private static Data Load()
    {
        if (_data != null) return _data;
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                _data = JsonSerializer.Deserialize<Data>(json) ?? new Data();
                return _data;
            }
        }
        catch { }
        _data = new Data();
        return _data;
    }

    private static void Persist()
    {
        try
        {
            Directory.CreateDirectory(FolderPath);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(_data));
        }
        catch { }
    }

    public static void MarkOnline(ulong userId)
    {
        lock (Lock)
        {
            Load().LastOnline[userId] = DateTimeOffset.UtcNow;
            Persist();
        }
    }

    public static void MarkVoice(ulong userId)
    {
        lock (Lock)
        {
            Load().LastVoice[userId] = DateTimeOffset.UtcNow;
            Persist();
        }
    }

    public static DateTimeOffset? GetLastOnline(ulong userId)
    {
        lock (Lock)
        {
            return Load().LastOnline.TryGetValue(userId, out var v) ? v : null;
        }
    }

    public static DateTimeOffset? GetLastVoice(ulong userId)
    {
        lock (Lock)
        {
            return Load().LastVoice.TryGetValue(userId, out var v) ? v : null;
        }
    }
}
