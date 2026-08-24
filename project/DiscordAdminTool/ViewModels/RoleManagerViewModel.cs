using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiscordAdminTool.Models;

namespace DiscordAdminTool.ViewModels;

public partial class RoleManagerViewModel : ViewModelBase
{
    public MainWindowViewModel Shell { get; }

    public ObservableCollection<RoleInfo> Roles { get; } = new();
    [ObservableProperty] private RoleInfo? _sourceRole;
    [ObservableProperty] private RoleInfo? _targetRole;
    [ObservableProperty] private int _actionTypeIndex;
    [ObservableProperty] private bool _onlyInactive;
    [ObservableProperty] private int _inactiveDays = 30;
    [ObservableProperty] private string _previewText = "";
    public bool IsReplace => ActionTypeIndex == 2;

    public RoleManagerViewModel(MainWindowViewModel shell)
    {
        Shell = shell;
        _ = LoadRolesAsync();
    }

    partial void OnActionTypeIndexChanged(int value) => OnPropertyChanged(nameof(IsReplace));

    public async Task LoadRolesAsync()
    {
        try
        {
            var roles = Shell.Discord.GetRoles();
            Roles.Clear();
            foreach (var r in roles) Roles.Add(r);
            if (SourceRole == null || !roles.Any(r => r.Id == SourceRole.Id)) SourceRole = roles.FirstOrDefault();
            if (TargetRole == null || !roles.Any(r => r.Id == TargetRole.Id)) TargetRole = roles.FirstOrDefault();
        }
        catch (Exception ex)
        {
            Shell.ShowToast("error", $"Error: {ex.Message}");
        }
    }

    private MemberFilter BuildFilter() => OnlyInactive
        ? new MemberFilter { InactiveDays = InactiveDays }
        : new MemberFilter();

    [RelayCommand]
    private async Task Preview()
    {
        if (TargetRole == null) return;
        try
        {
            var members = await Shell.Discord.GetRoleMembersAsync(TargetRole.Id);
            PreviewText = $"Miembros actuales con este rol: {members.Count}. El filtro se aplicara sobre usuarios que cumplan la condicion configurada.";
        }
        catch (Exception ex)
        {
            PreviewText = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task Execute()
    {
        if (TargetRole == null) return;
        var filter = BuildFilter();
        try
        {
            OperationResult result = ActionTypeIndex switch
            {
                0 => await Shell.Discord.AddRoleToUsersAsync(TargetRole.Id, filter, ConfigStoreMax()),
                1 => await Shell.Discord.RemoveRoleFromUsersAsync(TargetRole.Id, filter, ConfigStoreMax()),
                _ => await Shell.Discord.ReplaceRoleAsync(SourceRole!.Id, TargetRole.Id, filter, ConfigStoreMax()),
            };
            Shell.ShowToast("success", $"{result.AffectedCount} usuarios actualizados");
            await LoadRolesAsync();
        }
        catch (Exception ex)
        {
            Shell.ShowToast("error", $"Error: {ex.Message}");
        }
    }

    private static int ConfigStoreMax() => Services.ConfigStore.GetConfig().MaxBatchSize;

    public async Task DeleteRoleAsync(RoleInfo role)
    {
        try
        {
            await Shell.Discord.DeleteRoleAsync(role.Id);
            Shell.ShowToast("success", "Rol eliminado");
            await LoadRolesAsync();
        }
        catch (Exception ex)
        {
            Shell.ShowToast("error", $"Error: {ex.Message}");
        }
    }
}
