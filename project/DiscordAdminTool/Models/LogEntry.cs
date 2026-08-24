using System;
using System.Collections.Generic;

namespace DiscordAdminTool.Models;

public class LogEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public string Type { get; set; } = "";
    public string Executor { get; set; } = "admin";
    public int TargetCount { get; set; }
    public List<string> TargetIds { get; set; } = new();
    public Dictionary<string, object?> Details { get; set; } = new();
    public bool Success { get; set; } = true;
}

public class LogFilter
{
    public List<string> Types { get; set; } = new();
    public string? Search { get; set; }
}

public class LogPage
{
    public List<LogEntry> Items { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; } = 50;
}
