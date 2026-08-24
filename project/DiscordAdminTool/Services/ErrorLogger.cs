using System;
using System.IO;

namespace DiscordAdminTool.Services;

public static class ErrorLogger
{
    private static readonly string FolderPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "discord-admin-tool-app");
    private static readonly string FilePath = Path.Combine(FolderPath, "error.log");

    public static void Log(string context, Exception ex)
    {
        try
        {
            Directory.CreateDirectory(FolderPath);
            var line = $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}] {context}: {ex}\n";
            File.AppendAllText(FilePath, line);
        }
        catch { }
    }

    public static void Log(string message)
    {
        try
        {
            Directory.CreateDirectory(FolderPath);
            var line = $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}] {message}\n";
            File.AppendAllText(FilePath, line);
        }
        catch { }
    }
}
