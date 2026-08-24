using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiscordAdminTool.Models;

namespace DiscordAdminTool.ViewModels;

public partial class RoleFilterItem : ObservableObject
{
    public RoleInfo Role { get; }
    [ObservableProperty] private bool _isChecked;
    public RoleFilterItem(RoleInfo role) => Role = role;
}

public partial class MemberRow : ObservableObject
{
    public MemberInfo Member { get; }
    [ObservableProperty] private bool _isSelected;
    public string StatusLabel => Member.Status switch
    {
        MemberStatus.Active => "Activo",
        MemberStatus.Inactive => "Inactivo",
        MemberStatus.NeverSpoke => "Nunca hablo",
        _ => "Bot",
    };
    public string StatusVariant => Member.Status switch
    {
        MemberStatus.Active => "success",
        MemberStatus.Bot => "warning",
        _ => "danger",
    };
    public string LastMessageLabel => FormatRelative(Member.LastMessageAt);
    public string LastPresenceLabel => FormatRelative(Member.LastPresenceAt);
    public string LastVoiceLabel => FormatRelative(Member.LastVoiceAt);

    public MemberRow(MemberInfo member) => Member = member;

    private static string FormatRelative(DateTimeOffset? date)
    {
        if (date == null) return "Nunca";
        var span = DateTimeOffset.UtcNow - date.Value;
        if (span.TotalMinutes < 1) return "hace instantes";
        if (span.TotalMinutes < 60) return $"hace {(int)span.TotalMinutes} min";
        if (span.TotalHours < 24) return $"hace {(int)span.TotalHours} h";
        if (span.TotalDays < 30) return $"hace {(int)span.TotalDays} d";
        return $"hace {(int)(span.TotalDays / 30)} meses";
    }
}

public partial class UserManagerViewModel : ViewModelBase
{
    public MainWindowViewModel Shell { get; }
    private int _page = 1;
    private const int PageSize = 50;
    private CancellationTokenSource? _debounceCts;
    private readonly Dictionary<ulong, MemberInfo> _selectedUsers = new();

    [ObservableProperty] private string _search = "";
    public ObservableCollection<MemberRow> Rows { get; } = new();
    public ObservableCollection<RoleFilterItem> RoleFilters { get; } = new();
    [ObservableProperty] private int _total;
    [ObservableProperty] private int _totalPages = 1;
    [ObservableProperty] private bool _loading;
    [ObservableProperty] private string _pageLabel = "";
    [ObservableProperty] private int _selectedCount;
    [ObservableProperty] private bool _filterActive;
    [ObservableProperty] private bool _filterInactive;
    [ObservableProperty] private bool _filterNeverSpoke;
    [ObservableProperty] private bool _filterBot;

    public List<ulong> SelectedIds => _selectedUsers.Keys.ToList();
    public List<MemberInfo> SelectedMembers => _selectedUsers.Values.ToList();

    public UserManagerViewModel(MainWindowViewModel shell)
    {
        Shell = shell;
        _ = LoadRolesAsync();
        _ = LoadAsync();
    }

    private async Task LoadRolesAsync()
    {
        try
        {
            var roles = Shell.Discord.GetRoles();
            RoleFilters.Clear();
            foreach (var r in roles)
            {
                var item = new RoleFilterItem(r);
                item.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(RoleFilterItem.IsChecked)) ResetAndLoad(); };
                RoleFilters.Add(item);
            }
        }
        catch { }
        await Task.CompletedTask;
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

    partial void OnFilterActiveChanged(bool value) => ResetAndLoad();
    partial void OnFilterInactiveChanged(bool value) => ResetAndLoad();
    partial void OnFilterNeverSpokeChanged(bool value) => ResetAndLoad();
    partial void OnFilterBotChanged(bool value) => ResetAndLoad();

    private void ResetAndLoad()
    {
        _page = 1;
        _ = LoadAsync();
    }

    [RelayCommand]
    public async Task Refresh() => await LoadAsync();

    [RelayCommand]
    private async Task PrevPage()
    {
        if (_page > 1) { _page--; await LoadAsync(); }
    }

    [RelayCommand]
    private async Task NextPage()
    {
        if (_page < TotalPages) { _page++; await LoadAsync(); }
    }

    [RelayCommand]
    private void ToggleSelectAll()
    {
        var allSelected = Rows.Count > 0 && Rows.All(r => r.IsSelected);
        foreach (var row in Rows) row.IsSelected = !allSelected;
    }

    private MemberFilter BuildFilter()
    {
        var status = new List<MemberStatus>();
        if (FilterActive) status.Add(MemberStatus.Active);
        if (FilterInactive) status.Add(MemberStatus.Inactive);
        if (FilterNeverSpoke) status.Add(MemberStatus.NeverSpoke);
        if (FilterBot) status.Add(MemberStatus.Bot);
        var roleIds = RoleFilters.Where(r => r.IsChecked).Select(r => r.Role.Id).ToList();

        return new MemberFilter
        {
            Search = Search,
            Status = status.Count > 0 ? status : null,
            Roles = roleIds.Count > 0 ? roleIds : null,
        };
    }

    public async Task LoadAsync()
    {
        Loading = true;
        try
        {
            var result = await Shell.Discord.GetUsersAsync(BuildFilter(), _page, PageSize);
            Rows.Clear();
            foreach (var m in result.Items)
            {
                var row = new MemberRow(m) { IsSelected = _selectedUsers.ContainsKey(m.Id) };
                row.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName != nameof(MemberRow.IsSelected)) return;
                    if (row.IsSelected) _selectedUsers[row.Member.Id] = row.Member;
                    else _selectedUsers.Remove(row.Member.Id);
                    SelectedCount = _selectedUsers.Count;
                };
                Rows.Add(row);
            }
            Total = result.Total;
            TotalPages = Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
            PageLabel = $"Pagina {_page} de {TotalPages} ({Total} usuarios)";
        }
        catch (Exception ex)
        {
            Shell.ShowToast("error", $"Error: {ex.Message}");
        }
        finally
        {
            Loading = false;
        }
    }

    public void ClearSelection()
    {
        _selectedUsers.Clear();
        SelectedCount = 0;
        foreach (var row in Rows) row.IsSelected = false;
    }
}
