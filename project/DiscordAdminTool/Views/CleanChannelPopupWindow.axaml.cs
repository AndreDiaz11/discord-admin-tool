using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using DiscordAdminTool.Models;
using DiscordAdminTool.ViewModels;

namespace DiscordAdminTool.Views;

public partial class CleanChannelPopupWindow : Window
{
    private MainWindowViewModel? _shell;
    private ChannelInfo? _channel;
    private string _mode = "all";

    public CleanChannelPopupWindow()
    {
        InitializeComponent();
    }

    public CleanChannelPopupWindow(MainWindowViewModel shell, ChannelInfo channel) : this()
    {
        _shell = shell;
        _channel = channel;
        TitleText.Text = $"Limpiar #{channel.Name}";
    }

    private void SetMode(string mode)
    {
        _mode = mode;
        AllTab.Classes.Set("active", mode == "all");
        AmountTab.Classes.Set("active", mode == "amount");
        CloneTab.Classes.Set("active", mode == "clone");
        AmountPanel.IsVisible = mode == "amount";
        CloneInfoText.IsVisible = mode == "clone";
        ExecuteButton.Content = mode == "clone" ? "Clonar y Reemplazar" : "Limpiar";
    }

    private void AllTabClick(object? sender, RoutedEventArgs e) => SetMode("all");
    private void AmountTabClick(object? sender, RoutedEventArgs e) => SetMode("amount");
    private void CloneTabClick(object? sender, RoutedEventArgs e) => SetMode("clone");

    private void CancelClick(object? sender, RoutedEventArgs e) => Close(false);

    private async void ExecuteClick(object? sender, RoutedEventArgs e)
    {
        if (_shell == null || _channel == null) return;

        if (_mode == "clone")
        {
            var confirm = new ConfirmDialogWindow("Confirmar Clonar y Reemplazar",
                $"Se creara un canal nuevo identico a #{_channel.Name} y se borrara el original. Esta accion es irreversible.",
                "danger", "Clonar y Reemplazar", "Cancelar", true);
            var confirmed = await confirm.ShowDialog<bool>(this);
            if (!confirmed) return;

            ExecuteButton.IsEnabled = false;
            ExecuteButton.Content = "Procesando...";
            try
            {
                var result = await _shell.Discord.CloneAndReplaceChannelAsync(_channel.Id);
                _shell.ShowToast("success", result.MovedMembers > 0
                    ? $"Canal reemplazado ({result.MovedMembers} usuarios movidos)"
                    : "Canal reemplazado");
                Close(true);
            }
            catch (Exception ex)
            {
                _shell.ShowToast("error", $"Error: {ex.Message}");
                ExecuteButton.IsEnabled = true;
                ExecuteButton.Content = "Clonar y Reemplazar";
            }
            return;
        }

        var amount = _mode == "amount" ? (int)(AmountInput.Value ?? 50) : (int?)null;
        if (_mode == "amount" && (amount == null || amount < 1))
        {
            _shell.ShowToast("warning", "Ingresa una cantidad valida");
            return;
        }

        var body = _mode == "all"
            ? $"Se borraran TODOS los mensajes de #{_channel.Name}. Esta accion es irreversible."
            : $"Se borraran los ultimos {amount} mensajes de #{_channel.Name}. Esta accion es irreversible.";
        var confirmDelete = new ConfirmDialogWindow("Confirmar Limpieza de Canal", body, "danger", "Borrar", "Cancelar", true);
        var confirmedDelete = await confirmDelete.ShowDialog<bool>(this);
        if (!confirmedDelete) return;

        var progress = new ProgressPopupWindow(_channel.Name);
        var cancelling = false;

        progress.CancelRequested += async () =>
        {
            if (cancelling) return;
            var confirmCancel = new ConfirmDialogWindow("Cancelar Limpieza",
                "Los mensajes ya borrados no se pueden recuperar. ¿Seguro que quieres detener el resto?",
                "warning", "Si, cancelar", "Seguir borrando", false);
            var yes = await confirmCancel.ShowDialog<bool>(progress);
            if (!yes) return;
            cancelling = true;
            progress.Update(0, 0, true);
            _shell.Discord.CancelPurge(_channel.Id);
        };

        var progressTask = progress.ShowDialog(this);

        try
        {
            var result = await _shell.Discord.PurgeChannelMessagesAsync(_channel.Id, _mode, amount,
                (deleted, failed) => progress.Update(deleted, failed, cancelling));

            progress.Close();

            if (result.Cancelled)
                _shell.ShowToast("info", $"Cancelado: {result.DeletedCount} mensajes borrados antes de detener");
            else if (result.FailedCount > 0)
                _shell.ShowToast("warning", $"{result.DeletedCount} borrados, {result.FailedCount} fallidos. Motivo: {result.LastError ?? "desconocido"}", 0);
            else
                _shell.ShowToast("success", $"{result.DeletedCount} mensajes borrados");

            Close(true);
        }
        catch (Exception ex)
        {
            progress.Close();
            _shell.ShowToast("error", $"Error: {ex.Message}");
        }

        await progressTask;
    }
}
