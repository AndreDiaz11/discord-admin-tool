using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiscordAdminTool.Models;
using DiscordAdminTool.Services;

namespace DiscordAdminTool.ViewModels;

public enum ShellScreen { Welcome, GuildSelector, Main }

public partial class NavItem : ObservableObject
{
    public string Key { get; set; } = "";
    public string Icon { get; set; } = "";
    public string Label { get; set; } = "";
    [ObservableProperty] private bool _isActive;
}

public partial class MainWindowViewModel : ViewModelBase
{
    public DiscordService Discord { get; } = new();

    public ObservableCollection<NavItem> NavItems { get; } = new()
    {
        new NavItem { Key = "dashboard", Icon = "📊", Label = "Dashboard" },
        new NavItem { Key = "users", Icon = "👥", Label = "Usuarios" },
        new NavItem { Key = "roles", Icon = "🎭", Label = "Roles" },
        new NavItem { Key = "mass-actions", Icon = "🧹", Label = "Acciones Masivas" },
        new NavItem { Key = "channels", Icon = "🗑️", Label = "Limpiar Canales" },
        new NavItem { Key = "logs", Icon = "📜", Label = "Logs" },
        new NavItem { Key = "settings", Icon = "⚙️", Label = "Configuracion" },
    };

    [ObservableProperty] private ShellScreen _screen = ShellScreen.Welcome;
    [ObservableProperty] private string? _welcomeError;
    [ObservableProperty] private bool _connecting;
    [ObservableProperty] private string _tokenInput = "";
    [ObservableProperty] private ObservableCollection<GuildInfo> _guilds = new();
    [ObservableProperty] private string _botStatusLabel = "Bot conectado";
    [ObservableProperty] private string _botStatusKey = "connected";
    [ObservableProperty] private ViewModelBase? _currentSection;
    [ObservableProperty] private string _activeSectionKey = "dashboard";
    public ObservableCollection<ToastItem> Toasts { get; } = new();

    public MainWindowViewModel()
    {
        Discord.StatusChanged += status => Dispatcher.UIThread.Post(() => ApplyStatus(status));
        _ = BootstrapAsync();
    }

    private void ApplyStatus(string status)
    {
        BotStatusKey = status;
        BotStatusLabel = status switch
        {
            "connected" => "Bot conectado",
            "connecting" => "Conectando...",
            _ => "Desconectado",
        };
    }

    private async Task BootstrapAsync()
    {
        if (!TokenManager.HasToken())
        {
            Screen = ShellScreen.Welcome;
            return;
        }

        var token = TokenManager.GetToken();
        if (token == null)
        {
            Screen = ShellScreen.Welcome;
            return;
        }

        Connecting = true;
        try
        {
            await Discord.ConnectAsync(token);
            await AfterConnectAsync();
        }
        catch (Exception ex)
        {
            WelcomeError = ex.Message;
            Screen = ShellScreen.Welcome;
        }
        finally
        {
            Connecting = false;
        }
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        var token = TokenInput.Trim();
        if (string.IsNullOrEmpty(token)) return;

        Connecting = true;
        WelcomeError = null;
        try
        {
            await Discord.ConnectAsync(token);
            TokenManager.SaveToken(token);
            await AfterConnectAsync();
        }
        catch (Exception ex)
        {
            WelcomeError = ex.Message;
        }
        finally
        {
            Connecting = false;
        }
    }

    private async Task AfterConnectAsync()
    {
        var guildList = Discord.ListGuilds();
        Guilds = new ObservableCollection<GuildInfo>(guildList);
        var config = ConfigStore.GetConfig();
        var existing = guildList.FirstOrDefault(g => g.Id == config.GuildId);

        if (existing != null)
        {
            Discord.SelectGuild(existing.Id);
            OpenMainShell();
        }
        else
        {
            Screen = ShellScreen.GuildSelector;
        }
        await Task.CompletedTask;
    }

    [RelayCommand]
    private void SelectGuild(GuildInfo guild)
    {
        Discord.SelectGuild(guild.Id);
        var config = ConfigStore.GetConfig();
        config.GuildId = guild.Id;
        ConfigStore.SaveConfig(config);
        OpenMainShell();
    }

    private void OpenMainShell()
    {
        Screen = ShellScreen.Main;
        ApplyStatus(Discord.Status);
        Navigate("dashboard");
    }

    [RelayCommand]
    public void Navigate(string key)
    {
        if (CurrentSection is IDisposable disposable) disposable.Dispose();

        ActiveSectionKey = key;
        foreach (var item in NavItems) item.IsActive = item.Key == key;
        CurrentSection = key switch
        {
            "dashboard" => new DashboardViewModel(this),
            "users" => new UserManagerViewModel(this),
            "roles" => new RoleManagerViewModel(this),
            "mass-actions" => new MassActionsViewModel(this),
            "channels" => new ChannelCleanerViewModel(this),
            "logs" => new LogsViewerViewModel(this),
            "settings" => new SettingsPanelViewModel(this),
            _ => CurrentSection,
        };
    }

    public void ShowToast(string type, string message, int durationMs = -1)
    {
        var toast = new ToastItem { Type = type, Message = message };
        Dispatcher.UIThread.Post(() => Toasts.Add(toast));

        var duration = durationMs >= 0 ? durationMs : type switch
        {
            "success" => 5000,
            "warning" => 8000,
            "error" => 0,
            _ => 3000,
        };

        if (duration > 0)
        {
            _ = Task.Delay(duration).ContinueWith(_ => Dispatcher.UIThread.Post(() => Toasts.Remove(toast)));
        }
    }

    public void DismissToast(ToastItem toast) => Toasts.Remove(toast);

    [RelayCommand]
    private void Dismiss(ToastItem toast) => Toasts.Remove(toast);
}
