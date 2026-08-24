using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiscordAdminTool.Models;
using DiscordAdminTool.Services;

namespace DiscordAdminTool.ViewModels;

public partial class MassActionsViewModel : ViewModelBase
{
    public MainWindowViewModel Shell { get; }

    [ObservableProperty] private bool _filterByMessages = true;
    [ObservableProperty] private int _purgeDays = 30;
    [ObservableProperty] private bool _filterByConnection;
    [ObservableProperty] private int _purgeOfflineDays = 30;
    [ObservableProperty] private bool _excludeBots = true;
    [ObservableProperty] private bool _dryRun = true;
    [ObservableProperty] private string _purgeResultText = "";
    [ObservableProperty] private ObservableCollection<string> _previewLines = new();

    public ObservableCollection<RoleFilterItem> ExcludeRoleOptions { get; } = new();
    public ObservableCollection<RoleInfo> DmRoles { get; } = new();
    [ObservableProperty] private RoleInfo? _dmRole;
    [ObservableProperty] private string _dmMessage = "";

    public MassActionsViewModel(MainWindowViewModel shell)
    {
        Shell = shell;
        _ = LoadRolesAsync();
    }

    private async Task LoadRolesAsync()
    {
        try
        {
            var roles = Shell.Discord.GetRoles();
            ExcludeRoleOptions.Clear();
            DmRoles.Clear();
            foreach (var r in roles)
            {
                ExcludeRoleOptions.Add(new RoleFilterItem(r));
                DmRoles.Add(r);
            }
            DmRole = DmRoles.FirstOrDefault();
        }
        catch (Exception ex)
        {
            Shell.ShowToast("error", $"Error cargando roles: {ex.Message}");
        }
        await Task.CompletedTask;
    }

    private PurgeConfig BuildConfig() => new()
    {
        InactiveDays = FilterByMessages ? PurgeDays : null,
        OfflineDays = FilterByConnection ? PurgeOfflineDays : null,
        ExcludeRoles = ExcludeRoleOptions.Where(r => r.IsChecked).Select(r => r.Role.Id).ToList(),
        ExcludeBots = ExcludeBots,
        DryRun = true,
    };

    private void RenderPreview(PurgeResult result)
    {
        PurgeResultText = $"{result.AffectedCount} usuarios serian afectados";
        PreviewLines.Clear();
        foreach (var u in result.Preview.Take(50))
            PreviewLines.Add($"{u.Username} — msj: {(u.LastMessageAt?.ToString("g") ?? "Nunca")} — cuenta: {(u.LastPresenceAt?.ToString("g") ?? "Nunca")} — voz: {(u.LastVoiceAt?.ToString("g") ?? "Nunca")}");
    }

    [RelayCommand]
    private async Task PurgePreview()
    {
        if (!FilterByMessages && !FilterByConnection)
        {
            Shell.ShowToast("warning", "Activa al menos un filtro (mensajes o conexion)");
            return;
        }
        try
        {
            var result = await Shell.Discord.PreviewPurgeAsync(BuildConfig());
            RenderPreview(result);
        }
        catch (Exception ex)
        {
            PurgeResultText = $"Error: {ex.Message}";
        }
    }

    public async Task<PurgeResult?> PreviewForConfirmAsync()
    {
        if (!FilterByMessages && !FilterByConnection)
        {
            Shell.ShowToast("warning", "Activa al menos un filtro (mensajes o conexion)");
            return null;
        }
        return await Shell.Discord.PreviewPurgeAsync(BuildConfig());
    }

    public async Task ExecutePurgeAsync()
    {
        var config = BuildConfig();
        PurgeResultText = "Ejecutando purga...";
        try
        {
            var result = await Shell.Discord.ExecutePurgeAsync(new PurgeConfig
            {
                InactiveDays = config.InactiveDays,
                OfflineDays = config.OfflineDays,
                ExcludeRoles = config.ExcludeRoles,
                ExcludeBots = config.ExcludeBots,
                DryRun = DryRun,
            }, ConfigStore.GetConfig().MaxBatchSize);

            if (result.DryRun)
            {
                RenderPreview(result);
                Shell.ShowToast("info", "Modo simulacion activo: nadie fue expulsado");
            }
            else
            {
                Shell.ShowToast("success", $"{result.AffectedCount} usuarios expulsados, {result.FailedCount} fallidos");
                PurgeResultText = $"Purga completada: {result.AffectedCount} expulsados, {result.FailedCount} fallidos";
            }
        }
        catch (Exception ex)
        {
            Shell.ShowToast("error", $"Error: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task SendDm()
    {
        if (DmRole == null) return;
        if (string.IsNullOrWhiteSpace(DmMessage))
        {
            Shell.ShowToast("warning", "Escribe un mensaje antes de enviar");
            return;
        }
        try
        {
            var result = await Shell.Discord.SendMassDMAsync(DmRole.Id, DmMessage);
            Shell.ShowToast("success", $"{result.AffectedCount} mensajes enviados, {result.FailedCount} fallidos");
        }
        catch (Exception ex)
        {
            Shell.ShowToast("error", $"Error: {ex.Message}");
        }
    }
}
