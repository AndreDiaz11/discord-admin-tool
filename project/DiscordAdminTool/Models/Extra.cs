using System.Collections.Generic;

namespace DiscordAdminTool.Models;

public class DashboardData
{
    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }
    public int InactiveUsers { get; set; }
    public int BotUsers { get; set; }
    public int TotalRoles { get; set; }
    public List<LogEntry> RecentLogs { get; set; } = new();
}

public class MemberPage
{
    public List<MemberInfo> Items { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; } = 50;
}

public class PurgeChannelResult
{
    public bool Success { get; set; } = true;
    public int DeletedCount { get; set; }
    public int FailedCount { get; set; }
    public string? LastError { get; set; }
    public bool Cancelled { get; set; }
}

public class CloneChannelResult
{
    public bool Success { get; set; } = true;
    public ulong NewChannelId { get; set; }
    public int MovedMembers { get; set; }
    public int FailedMoves { get; set; }
}

public class BatchResult<T>
{
    public List<T> Results { get; set; } = new();
    public List<OperationFailure> Failures { get; set; } = new();
}
