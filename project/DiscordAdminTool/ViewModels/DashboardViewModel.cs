using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiscordAdminTool.Models;

namespace DiscordAdminTool.ViewModels;

public class ActionCard
{
    public string Key { get; set; } = "";
    public string Icon { get; set; } = "";
    public string Title { get; set; } = "";
    public string Desc { get; set; } = "";
}

public partial class DashboardViewModel : ViewModelBase, IDisposable
{
    private readonly MainWindowViewModel _shell;
    private readonly DispatcherTimer _timer;

    [ObservableProperty] private bool _loaded;
    [ObservableProperty] private string _errorText = "";
    [ObservableProperty] private int _totalUsers;
    [ObservableProperty] private int _botUsers;
    [ObservableProperty] private int _activeUsers;
    [ObservableProperty] private int _inactiveUsers;
    [ObservableProperty] private int _totalRoles;
    [ObservableProperty] private double _activeFraction;
    public ObservableCollection<LogEntry> RecentLogs { get; } = new();

    public ObservableCollection<ActionCard> ActionCards { get; } = new()
    {
        new ActionCard { Key = "mass-actions", Icon = "🧹", Title = "Purga por Inactividad", Desc = "Expulsar usuarios inactivos" },
        new ActionCard { Key = "roles", Icon = "🎭", Title = "Gestion Masiva de Roles", Desc = "Asignar, quitar o reemplazar roles" },
        new ActionCard { Key = "mass-actions", Icon = "✉️", Title = "Mensaje de Bienvenida", Desc = "Enviar mensaje masivo por rol" },
        new ActionCard { Key = "roles", Icon = "💾", Title = "Backup de Roles", Desc = "Crear copia de seguridad de roles" },
    };

    public DashboardViewModel(MainWindowViewModel shell)
    {
        _shell = shell;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        _timer.Tick += async (_, _) => await RefreshAsync();
        _timer.Start();
        _ = RefreshAsync();
    }

    [RelayCommand]
    private void OpenCard(ActionCard card) => _shell.Navigate(card.Key);

    private async Task RefreshAsync()
    {
        try
        {
            var data = await _shell.Discord.GetDashboardDataAsync();
            TotalUsers = data.TotalUsers;
            BotUsers = data.BotUsers;
            ActiveUsers = data.ActiveUsers;
            InactiveUsers = data.InactiveUsers;
            TotalRoles = data.TotalRoles;
            var total = data.ActiveUsers + data.InactiveUsers;
            ActiveFraction = total > 0 ? (double)data.ActiveUsers / total : 0;

            RecentLogs.Clear();
            foreach (var log in data.RecentLogs) RecentLogs.Add(log);

            Loaded = true;
        }
        catch (Exception ex)
        {
            if (!Loaded) ErrorText = $"No se pudieron cargar los datos: {ex.Message}";
        }
    }

    public void Dispose() => _timer.Stop();
}
