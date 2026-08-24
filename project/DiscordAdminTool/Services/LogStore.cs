using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using DiscordAdminTool.Models;

namespace DiscordAdminTool.Services;

public static class LogStore
{
    private static readonly string FolderPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "discord-admin-tool-app");
    private static readonly string FilePath = Path.Combine(FolderPath, "action-logs.json");
    private const int MaxEntries = 5000;
    private static List<LogEntry>? _cached;
    private static readonly object Lock = new();

    private static List<LogEntry> Load()
    {
        if (_cached != null) return _cached;
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                _cached = JsonSerializer.Deserialize<List<LogEntry>>(json) ?? new List<LogEntry>();
                return _cached;
            }
        }
        catch { }
        _cached = new List<LogEntry>();
        return _cached;
    }

    private static void Persist()
    {
        try
        {
            Directory.CreateDirectory(FolderPath);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(_cached, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    public static LogEntry Add(LogEntry entry)
    {
        lock (Lock)
        {
            var entries = Load();
            entries.Insert(0, entry);
            if (entries.Count > MaxEntries) entries.RemoveRange(MaxEntries, entries.Count - MaxEntries);
            Persist();
            return entry;
        }
    }

    public static LogPage List(LogFilter? filter, int page = 1, int pageSize = 50)
    {
        lock (Lock)
        {
            IEnumerable<LogEntry> entries = Load();
            filter ??= new LogFilter();

            if (filter.Types.Count > 0)
                entries = entries.Where(e => filter.Types.Contains(e.Type));

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var q = filter.Search.ToLowerInvariant();
                entries = entries.Where(e => JsonSerializer.Serialize(e).ToLowerInvariant().Contains(q));
            }

            var list = entries.ToList();
            var total = list.Count;
            var start = Math.Max(0, (page - 1) * pageSize);
            var items = list.Skip(start).Take(pageSize).ToList();

            return new LogPage { Items = items, Total = total, Page = page, PageSize = pageSize };
        }
    }

    public static void Clear()
    {
        lock (Lock)
        {
            _cached = new List<LogEntry>();
            Persist();
        }
    }
}
