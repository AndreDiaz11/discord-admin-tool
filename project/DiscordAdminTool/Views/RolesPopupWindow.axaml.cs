using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using DiscordAdminTool.Models;
using DiscordAdminTool.Services;
using DiscordAdminTool.ViewModels;

namespace DiscordAdminTool.Views;

public partial class RolesPopupWindow : Window
{
    private MainWindowViewModel? _shell;
    private List<MemberInfo> _users = new();
    private List<RoleInfo> _roles = new();
    private string _mode = "add";
    private readonly Dictionary<ulong, CheckBox> _checks = new();

    public RolesPopupWindow()
    {
        InitializeComponent();
    }

    public RolesPopupWindow(MainWindowViewModel shell, List<MemberInfo> users) : this()
    {
        _shell = shell;
        _users = users;
        TitleText.Text = $"Gestionar Roles — {users.Count} seleccionados";
        try { _roles = shell.Discord.GetRoles(); } catch { _roles = new(); }
        Paint();
    }

    private bool IsLocked(RoleInfo role)
    {
        var hasCount = _users.Count(u => u.Roles.Any(r => r.Id == role.Id));
        return _mode == "add" ? (_users.Count > 0 && hasCount == _users.Count) : hasCount == 0;
    }

    private void Paint()
    {
        _checks.Clear();
        var panel = new StackPanel { Spacing = 8 };

        if (_roles.Count == 0)
        {
            panel.Children.Add(new TextBlock { Text = "Sin roles", Classes = { "empty-state" } });
        }
        else
        {
            foreach (var role in _roles)
            {
                var locked = IsLocked(role);
                var check = new CheckBox
                {
                    Content = role.Name,
                    Foreground = Brushes.White,
                    IsEnabled = !locked,
                    Opacity = locked ? 0.35 : 1,
                };
                _checks[role.Id] = check;
                panel.Children.Add(check);
            }
        }

        RolesList.Items.Clear();
        RolesList.Items.Add(panel);
    }

    private void AddTabClick(object? sender, RoutedEventArgs e)
    {
        _mode = "add";
        AddTab.Classes.Add("active");
        RemoveTab.Classes.Remove("active");
        Paint();
    }

    private void RemoveTabClick(object? sender, RoutedEventArgs e)
    {
        _mode = "remove";
        RemoveTab.Classes.Add("active");
        AddTab.Classes.Remove("active");
        Paint();
    }

    private void CancelClick(object? sender, RoutedEventArgs e) => Close(false);

    private async void ApplyClick(object? sender, RoutedEventArgs e)
    {
        var checkedRoleIds = _checks.Where(kv => kv.Value.IsChecked == true).Select(kv => kv.Key).ToList();
        if (checkedRoleIds.Count == 0)
        {
            _shell?.ShowToast("warning", "Marca al menos un rol");
            return;
        }

        var confirm = new ConfirmDialogWindow(
            _mode == "add" ? "Confirmar Agregar Roles" : "Confirmar Quitar Roles",
            $"Se {(_mode == "add" ? "agregaran" : "quitaran")} {checkedRoleIds.Count} rol(es) a {_users.Count} usuarios.",
            "warning", "Aplicar", "Cancelar", _users.Count > 50);
        var confirmed = await confirm.ShowDialog<bool>(this);
        if (!confirmed) return;

        ApplyButton.IsEnabled = false;
        var ids = _users.Select(u => u.Id).ToList();
        var maxBatch = ConfigStore.GetConfig().MaxBatchSize;
        var affected = 0;
        var failures = new List<OperationFailure>();

        try
        {
            foreach (var roleId in checkedRoleIds)
            {
                var filter = new MemberFilter { Ids = ids };
                var result = _mode == "add"
                    ? await _shell!.Discord.AddRoleToUsersAsync(roleId, filter, maxBatch)
                    : await _shell!.Discord.RemoveRoleFromUsersAsync(roleId, filter, maxBatch);
                affected += result.AffectedCount;
                failures.AddRange(result.Failures);
            }

            if (failures.Count > 0)
                _shell?.ShowToast("warning", $"{affected} cambios, {failures.Count} fallidos. Motivo: {failures[0].Reason}", 0);
            else
                _shell?.ShowToast("success", $"{affected} cambios aplicados");

            Close(true);
        }
        catch (System.Exception ex)
        {
            _shell?.ShowToast("error", $"Error: {ex.Message}");
            ApplyButton.IsEnabled = true;
        }
    }
}
