namespace DiscordAdminTool.Models;

public class AppConfig
{
    public ulong? GuildId { get; set; }
    public int DefaultInactiveDays { get; set; } = 30;
    public ulong? LogChannelId { get; set; }
    public bool NotifyOnAction { get; set; }
    public bool DryRunDefault { get; set; } = true;
    public bool ConfirmDestructive { get; set; } = true;
    public int MaxBatchSize { get; set; } = 1000;
    public bool AnimationsEnabled { get; set; } = true;
    public bool CompactMode { get; set; }
    public bool ShowAvatars { get; set; } = true;
    public bool AutoPurgeEnabled { get; set; }
}
