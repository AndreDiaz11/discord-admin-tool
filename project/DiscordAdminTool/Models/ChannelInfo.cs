namespace DiscordAdminTool.Models;

public enum ChannelKind { Text, Voice, Announcement }

public class ChannelInfo
{
    public ulong Id { get; set; }
    public string Name { get; set; } = "";
    public ChannelKind Type { get; set; }
    public int MessageCount { get; set; }
    public bool MessageCountCapped { get; set; }
    public bool? HasContent { get; set; }
    public string? Error { get; set; }
}
