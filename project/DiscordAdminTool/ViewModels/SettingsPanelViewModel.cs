using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiscordAdminTool.Models;
using DiscordAdminTool.Services;

namespace DiscordAdminTool.ViewModels;

public partial class SettingsPanelViewModel : ViewModelBase
{
    public MainWindowViewModel Shell { get; }

    [ObservableProperty] private int _defaultInactiveDays;
    [ObservableProperty] private bool _notifyOnAction;
    [ObservableProperty] private bool _dryRunDefault;
    [ObservableProperty] private bool _confirmDestructive;
    [ObservableProperty] private int _maxBatchSize;
    [ObservableProperty] private bool _animationsEnabled;
    [ObservableProperty] private bool _compactMode;
    [ObservableProperty] private bool _showAvatars;
    [ObservableProperty] private string _guildIdLabel = "Ninguno";

    public SettingsPanelViewModel(MainWindowViewModel shell)
    {
        Shell = shell;
        var config = ConfigStore.GetConfig();
        ApplyFrom(config);
    }

    private void ApplyFrom(AppConfig config)
    {
        DefaultInactiveDays = config.DefaultInactiveDays;
        NotifyOnAction = config.NotifyOnAction;
        DryRunDefault = config.DryRunDefault;
        ConfirmDestructive = config.ConfirmDestructive;
        MaxBatchSize = config.MaxBatchSize;
        AnimationsEnabled = config.AnimationsEnabled;
        CompactMode = config.CompactMode;
        ShowAvatars = config.ShowAvatars;
        GuildIdLabel = config.GuildId?.ToString() ?? "Ninguno";
    }

    [RelayCommand]
    private void Save()
    {
        var config = ConfigStore.GetConfig();
        config.DefaultInactiveDays = DefaultInactiveDays;
        config.NotifyOnAction = NotifyOnAction;
        config.DryRunDefault = DryRunDefault;
        config.ConfirmDestructive = ConfirmDestructive;
        config.MaxBatchSize = MaxBatchSize;
        config.AnimationsEnabled = AnimationsEnabled;
        config.CompactMode = CompactMode;
        config.ShowAvatars = ShowAvatars;
        ConfigStore.SaveConfig(config);
        Shell.ShowToast("success", "Configuracion guardada");
    }

    [RelayCommand]
    private void ResetDefaults()
    {
        var config = ConfigStore.ResetToDefaults();
        ApplyFrom(config);
        Shell.ShowToast("success", "Configuracion restaurada");
    }

    public async Task ClearTokenAsync()
    {
        TokenManager.ClearToken();
        Shell.ShowToast("success", "Token eliminado");
        await Task.CompletedTask;
    }
}
