using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiscordAdminTool.Models;
using DiscordAdminTool.Services;

namespace DiscordAdminTool.ViewModels;

public partial class LogTypeFilter : ObservableObject
{
    public string Type { get; }
    [ObservableProperty] private bool _isSelected;
    public LogTypeFilter(string type) => Type = type;
}

public partial class LogsViewerViewModel : ViewModelBase
{
    public MainWindowViewModel Shell { get; }
    private int _page = 1;
    private CancellationTokenSource? _debounceCts;
    private LogPage _lastResult = new();

    public static readonly string[] TypeList =
    {
        "kick", "ban", "timeout", "role_add", "role_remove", "purge", "channel_clear", "channel_clone", "dm", "config_change", "join",
    };

    public ObservableCollection<LogTypeFilter> TypeFilters { get; } = new();
    public ObservableCollection<LogEntry> Items { get; } = new();
    [ObservableProperty] private string _search = "";
    [ObservableProperty] private string _pageLabel = "";
    [ObservableProperty] private bool _loading;

    public LogsViewerViewModel(MainWindowViewModel shell)
    {
        Shell = shell;
        foreach (var t in TypeList)
        {
            var f = new LogTypeFilter(t);
            f.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(LogTypeFilter.IsSelected)) ResetAndLoad(); };
            TypeFilters.Add(f);
        }
        _ = LoadAsync();
    }

    partial void OnSearchChanged(string value)
    {
        _debounceCts?.Cancel();
        _debounceCts = new CancellationTokenSource();
        var token = _debounceCts.Token;
        _ = Task.Delay(300, token).ContinueWith(t =>
        {
            if (!t.IsCanceled) Dispatcher.UIThread.Post(ResetAndLoad);
        }, TaskScheduler.Default);
    }

    private void ResetAndLoad()
    {
        _page = 1;
        _ = LoadAsync();
    }

    private LogFilter BuildFilter() => new()
    {
        Types = TypeFilters.Where(t => t.IsSelected).Select(t => t.Type).ToList(),
        Search = Search,
    };

    public async Task LoadAsync()
    {
        Loading = true;
        try
        {
            _lastResult = LogStore.List(BuildFilter(), _page);
            Items.Clear();
            foreach (var e in _lastResult.Items) Items.Add(e);
            var totalPages = Math.Max(1, (int)Math.Ceiling(_lastResult.Total / (double)_lastResult.PageSize));
            PageLabel = $"Pagina {_page} de {totalPages}";
        }
        finally
        {
            Loading = false;
        }
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task PrevPage()
    {
        if (_page > 1) { _page--; await LoadAsync(); }
    }

    [RelayCommand]
    private async Task NextPage()
    {
        var totalPages = Math.Max(1, (int)Math.Ceiling(_lastResult.Total / (double)_lastResult.PageSize));
        if (_page < totalPages) { _page++; await LoadAsync(); }
    }

    public async Task ClearLogsAsync()
    {
        LogStore.Clear();
        Shell.ShowToast("success", "Logs eliminados");
        await LoadAsync();
    }
}
