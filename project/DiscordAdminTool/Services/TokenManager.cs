using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace DiscordAdminTool.Services;

public static class TokenManager
{
    private static readonly string FolderPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "discord-admin-tool-app");
    private static readonly string FilePath = Path.Combine(FolderPath, "secure-token.dat");

    public static bool HasToken() => File.Exists(FilePath);

    public static void SaveToken(string token)
    {
        Directory.CreateDirectory(FolderPath);
        var plain = Encoding.UTF8.GetBytes(token);
        var protectedBytes = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(FilePath, protectedBytes);
    }

    public static string? GetToken()
    {
        if (!HasToken()) return null;
        try
        {
            var protectedBytes = File.ReadAllBytes(FilePath);
            var plain = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plain);
        }
        catch
        {
            return null;
        }
    }

    public static void ClearToken()
    {
        if (File.Exists(FilePath)) File.Delete(FilePath);
    }
}
