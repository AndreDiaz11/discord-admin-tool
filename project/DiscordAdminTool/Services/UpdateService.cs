using System.Threading.Tasks;
using Velopack;
using Velopack.Sources;

namespace DiscordAdminTool.Services;

public class UpdateCheckResult
{
    public bool Available { get; init; }
    public string? Version { get; init; }
}

public static class UpdateService
{
    private const string RepoUrl = "https://github.com/AndreDiaz11/discord-admin-tool";

    private static UpdateManager? _manager;
    private static UpdateInfo? _pendingUpdate;

    private static UpdateManager Manager => _manager ??= new UpdateManager(new GithubSource(RepoUrl, null, false));

    public static async Task<UpdateCheckResult> CheckAsync()
    {
        try
        {
            if (!Manager.IsInstalled) return new UpdateCheckResult { Available = false };

            var info = await Manager.CheckForUpdatesAsync();
            if (info is null) return new UpdateCheckResult { Available = false };

            _pendingUpdate = info;
            return new UpdateCheckResult { Available = true, Version = info.TargetFullRelease.Version.ToString() };
        }
        catch
        {
            return new UpdateCheckResult { Available = false };
        }
    }

    public static async Task DownloadAndApplyAsync()
    {
        if (_pendingUpdate is null) return;
        await Manager.DownloadUpdatesAsync(_pendingUpdate);
        Manager.ApplyUpdatesAndRestart(_pendingUpdate);
    }
}
