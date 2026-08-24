namespace DiscordAdminTool.Models;

public class GuildInfo
{
    public ulong Id { get; set; }
    public string Name { get; set; } = "";
    public string? IconUrl { get; set; }
    public int MemberCount { get; set; }
}
