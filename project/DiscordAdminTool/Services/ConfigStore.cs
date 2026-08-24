using System;
using System.IO;
using System.Text.Json;
using DiscordAdminTool.Models;

namespace DiscordAdminTool.Services;

public static class ConfigStore
{
    private static readonly string FolderPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "discord-admin-tool-app");
    private static readonly string FilePath = Path.Combine(FolderPath, "config.json");
    private static AppConfig? _cached;

    public static AppConfig GetConfig()
    {
        if (_cached != null) return _cached;
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                _cached = JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
                return _cached;
            }
        }
        catch { }
        _cached = new AppConfig();
        return _cached;
    }

    public static AppConfig SaveConfig(AppConfig config)
    {
        _cached = config;
        try
        {
            Directory.CreateDirectory(FolderPath);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
        return config;
    }

    public static AppConfig ResetToDefaults()
    {
        _cached = new AppConfig();
        SaveConfig(_cached);
        return _cached;
    }
}
