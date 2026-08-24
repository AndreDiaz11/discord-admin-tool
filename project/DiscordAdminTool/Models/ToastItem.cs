namespace DiscordAdminTool.Models;

public class ToastItem
{
    public string Type { get; set; } = "info";
    public string Message { get; set; } = "";
    public string Icon => Type switch
    {
        "success" => "✅",
        "warning" => "⚠️",
        "error" => "❌",
        _ => "ℹ️",
    };
}
