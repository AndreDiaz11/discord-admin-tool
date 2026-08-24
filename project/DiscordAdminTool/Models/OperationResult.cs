using System.Collections.Generic;

namespace DiscordAdminTool.Models;

public class OperationFailure
{
    public string Item { get; set; } = "";
    public string Reason { get; set; } = "";
}

public class OperationResult
{
    public bool Success { get; set; } = true;
    public int AffectedCount { get; set; }
    public int FailedCount { get; set; }
    public List<OperationFailure> Failures { get; set; } = new();
}

public class PurgeConfig
{
    public int? InactiveDays { get; set; }
    public int? OfflineDays { get; set; }
    public List<ulong> ExcludeRoles { get; set; } = new();
    public bool ExcludeBots { get; set; } = true;
    public bool DryRun { get; set; } = true;
}

public class PurgeResult : OperationResult
{
    public long ExecutionTimeMs { get; set; }
    public bool DryRun { get; set; }
    public List<MemberInfo> Preview { get; set; } = new();
}

public class MemberFilter
{
    public List<ulong>? Ids { get; set; }
    public string? Search { get; set; }
    public List<ulong>? Roles { get; set; }
    public bool ExcludeBots { get; set; }
    public List<ulong>? ExcludeRoles { get; set; }
    public int? InactiveDays { get; set; }
    public List<MemberStatus>? Status { get; set; }
}

public class RoleBackupRole
{
    public ulong Id { get; set; }
    public string Name { get; set; } = "";
    public string Color { get; set; } = "#8e9297";
    public int Position { get; set; }
}

public class MemberRoleEntry
{
    public ulong UserId { get; set; }
    public List<ulong> RoleIds { get; set; } = new();
}

public class RoleBackup
{
    public ulong GuildId { get; set; }
    public string CreatedAt { get; set; } = "";
    public List<RoleBackupRole> Roles { get; set; } = new();
    public List<MemberRoleEntry> MemberRoles { get; set; } = new();
}
