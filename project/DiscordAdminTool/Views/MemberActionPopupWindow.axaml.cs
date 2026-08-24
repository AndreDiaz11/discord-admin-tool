using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using DiscordAdminTool.Services;
using DiscordAdminTool.ViewModels;

namespace DiscordAdminTool.Views;

public partial class MemberActionPopupWindow : Window
{
    private MainWindowViewModel? _shell;
    private List<ulong> _ids = new();
    private string _action = "kick";

    public MemberActionPopupWindow()
    {
        InitializeComponent();
        BanDaysCombo.SelectedIndex = 0;
        DurationCombo.SelectedIndex = 0;
    }

    public MemberActionPopupWindow(MainWindowViewModel shell, List<ulong> ids) : this()
    {
        _shell = shell;
        _ids = ids;
        TitleText.Text = $"Acciones sobre Miembros — {ids.Count} seleccionados";
    }

    private void SetAction(string action)
    {
        _action = action;
        KickTab.Classes.Set("active", action == "kick");
        BanTab.Classes.Set("active", action == "ban");
        TimeoutTab.Classes.Set("active", action == "timeout");
        BanDaysPanel.IsVisible = action == "ban";
        DurationPanel.IsVisible = action == "timeout";
        ExecuteButton.Content = action switch { "kick" => "Expulsar", "ban" => "Banear", _ => "Suspender" };
    }

    private void KickTabClick(object? sender, RoutedEventArgs e) => SetAction("kick");
    private void BanTabClick(object? sender, RoutedEventArgs e) => SetAction("ban");
    private void TimeoutTabClick(object? sender, RoutedEventArgs e) => SetAction("timeout");

    private void CancelClick(object? sender, RoutedEventArgs e) => Close(false);

    private async void ExecuteClick(object? sender, RoutedEventArgs e)
    {
        var reason = string.IsNullOrWhiteSpace(ReasonBox.Text) ? "Accion masiva desde panel admin" : ReasonBox.Text.Trim();
        var title = _action switch { "kick" => "Confirmar Expulsion", "ban" => "Confirmar Baneo", _ => "Confirmar Suspension" };
        var body = $"Esta accion afectara a {_ids.Count} usuarios." + (_action != "timeout" ? " Esta accion puede ser irreversible." : "");
        var requireTyped = _ids.Count > 50 || _action == "ban";

        var confirm = new ConfirmDialogWindow(title, body, "danger", "Confirmar", "Cancelar", requireTyped);
        var confirmed = await confirm.ShowDialog<bool>(this);
        if (!confirmed) return;

        ExecuteButton.IsEnabled = false;
        var maxBatch = ConfigStore.GetConfig().MaxBatchSize;

        try
        {
            if (_action == "kick")
            {
                var result = await _shell!.Discord.KickUsersAsync(_ids, reason, maxBatch);
                _shell.ShowToast("success", $"{result.AffectedCount} afectados, {result.FailedCount} fallidos");
            }
            else if (_action == "ban")
            {
                var days = int.Parse((string)((ComboBoxItem)BanDaysCombo.SelectedItem!).Tag!);
                var result = await _shell!.Discord.BanUsersAsync(_ids, reason, days * 86400, maxBatch);
                _shell.ShowToast("success", $"{result.AffectedCount} afectados, {result.FailedCount} fallidos");
            }
            else
            {
                var duration = long.Parse((string)((ComboBoxItem)DurationCombo.SelectedItem!).Tag!);
                var result = await _shell!.Discord.TimeoutUsersAsync(_ids, duration, reason, maxBatch);
                _shell.ShowToast("success", $"{result.AffectedCount} afectados, {result.FailedCount} fallidos");
            }
            Close(true);
        }
        catch (Exception ex)
        {
            _shell?.ShowToast("error", $"Error: {ex.Message}");
            ExecuteButton.IsEnabled = true;
        }
    }
}
