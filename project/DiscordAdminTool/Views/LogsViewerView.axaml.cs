using System;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using DiscordAdminTool.Models;
using DiscordAdminTool.Services;
using DiscordAdminTool.ViewModels;

namespace DiscordAdminTool.Views;

public partial class LogsViewerView : UserControl
{
    private LogsViewerViewModel? Vm => DataContext as LogsViewerViewModel;

    public LogsViewerView()
    {
        InitializeComponent();
    }

    private Window? Owner => TopLevel.GetTopLevel(this) as Window;

    private async void ExportClick(object? sender, RoutedEventArgs e)
    {
        if (Vm == null || Owner == null) return;
        try
        {
            var filter = new LogFilter { Types = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Select(
                System.Linq.Enumerable.Where(Vm.TypeFilters, t => t.IsSelected), t => t.Type)), Search = Vm.Search };
            var all = new System.Collections.Generic.List<Models.LogEntry>();
            var page = 1;
            var total = int.MaxValue;
            while (all.Count < total && page <= 100)
            {
                var result = LogStore.List(filter, page);
                all.AddRange(result.Items);
                total = result.Total;
                page++;
            }

            var file = await Owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                SuggestedFileName = $"logs-export-{DateTime.Now:yyyyMMdd-HHmmss}",
                DefaultExtension = "json",
                FileTypeChoices = new[] { new FilePickerFileType("JSON") { Patterns = new[] { "*.json" } } },
            });
            if (file == null) return;

            await using var stream = await file.OpenWriteAsync();
            await JsonSerializer.SerializeAsync(stream, all, new JsonSerializerOptions { WriteIndented = true });
            Vm.Shell.ShowToast("success", $"{all.Count} registros exportados");
        }
        catch (Exception ex)
        {
            Vm.Shell.ShowToast("error", $"Error: {ex.Message}");
        }
    }

    private async void ClearClick(object? sender, RoutedEventArgs e)
    {
        if (Vm == null || Owner == null) return;
        var confirm = new ConfirmDialogWindow("Limpiar Logs",
            "Se eliminaran permanentemente todos los registros de actividad guardados.",
            "danger", "Eliminar todo", "Cancelar", true);
        var confirmed = await confirm.ShowDialog<bool>(Owner);
        if (!confirmed) return;
        await Vm.ClearLogsAsync();
    }
}
