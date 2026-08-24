using System;
using System.Collections.Generic;

namespace DiscordAdminTool.Models;

public enum MemberStatus { Active, Inactive, NeverSpoke, Bot }

public class RoleChip
{
    public ulong Id { get; set; }
    public string Name { get; set; } = "";
    public string Color { get; set; } = "#8e9297";
}

public class MemberInfo
{
    public ulong Id { get; set; }
    public string Username { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Avatar { get; set; } = "";
    public DateTimeOffset? JoinedAt { get; set; }
    public DateTimeOffset? LastMessageAt { get; set; }
    public DateTimeOffset? LastPresenceAt { get; set; }
    public DateTimeOffset? LastVoiceAt { get; set; }
    public List<RoleChip> Roles { get; set; } = new();
    public MemberStatus Status { get; set; }
    public bool IsBot { get; set; }
}
