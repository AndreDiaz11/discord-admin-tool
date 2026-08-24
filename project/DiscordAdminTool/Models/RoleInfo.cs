namespace DiscordAdminTool.Models;

public class RoleInfo
{
    public ulong Id { get; set; }
    public string Name { get; set; } = "";
    public string Color { get; set; } = "#8e9297";
    public int Position { get; set; }
    public int MemberCount { get; set; }
    public bool IsManaged { get; set; }
}
