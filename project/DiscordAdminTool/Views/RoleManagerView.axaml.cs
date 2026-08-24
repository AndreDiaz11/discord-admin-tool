using System;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using DiscordAdminTool.Models;
using DiscordAdminTool.ViewModels;

namespace DiscordAdminTool.Views;

public partial class RoleManagerView : UserControl
{
    private RoleManagerViewModel? Vm => DataContext as RoleManagerViewModel;

    public RoleManagerView()
    {
        InitializeComponent();
    }

    private Window? Owner => TopLevel.GetTopLevel(this) as Window;

    private async void CreateRoleClick(object? sender, RoutedEventArgs e)
    {
        if (Vm == null || Owner == null) return;
        var popup = new RoleFormPopupWindow("Crear Rol", null);
        var result = await popup.ShowDialog<RoleFormResult?>(Owner);
        if (result == null) return;
        try
        {
            await Vm.Shell.Discord.CreateRoleAsync(result.Name, result.Color, result.Hoist, result.Mentionable);
            Vm.Shell.ShowToast("success", $"Rol \"{result.Name}\" creado");
            await Vm.LoadRolesAsync();
        }
        catch (Exception ex)
        {
            Vm.Shell.ShowToast("error", $"Error: {ex.Message}");
        }
    }

    private async void EditRoleClick(object? sender, RoutedEventArgs e)
    {
        if (Vm == null || Owner == null || sender is not Button { Tag: RoleInfo role }) return;
        var popup = new RoleFormPopupWindow($"Editar Rol: {role.Name}", role);
        var result = await popup.ShowDialog<RoleFormResult?>(Owner);
        if (result == null) return;
        try
        {
            await Vm.Shell.Discord.EditRoleAsync(role.Id, result.Name, result.Color, result.Hoist, result.Mentionable);
            Vm.Shell.ShowToast("success", "Rol actualizado");
            await Vm.LoadRolesAsync();
        }
        catch (Exception ex)
        {
            Vm.Shell.ShowToast("error", $"Error: {ex.Message}");
        }
    }

    private async void DeleteRoleClick(object? sender, RoutedEventArgs e)
    {
        if (Vm == null || Owner == null || sender is not Button { Tag: RoleInfo role }) return;
        var confirm = new ConfirmDialogWindow("Confirmar Eliminar Rol",
            $"Se eliminara el rol \"{role.Name}\" de {role.MemberCount} miembros que lo tienen. Esta accion es irreversible.",
            "danger", "Eliminar", "Cancelar", true);
        var confirmed = await confirm.ShowDialog<bool>(Owner);
        if (!confirmed) return;
        await Vm.DeleteRoleAsync(role);
    }

    private async void CreateBackupClick(object? sender, RoutedEventArgs e)
    {
        if (Vm == null || Owner == null) return;
        try
        {
            var backup = await Vm.Shell.Discord.CreateRoleBackupAsync();
            var file = await Owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                SuggestedFileName = $"roles-backup-{DateTime.Now:yyyyMMdd-HHmmss}",
                DefaultExtension = "json",
                FileTypeChoices = new[] { new FilePickerFileType("JSON") { Patterns = new[] { "*.json" } } },
            });
            if (file == null) return;
            await using var stream = await file.OpenWriteAsync();
            await JsonSerializer.SerializeAsync(stream, backup, new JsonSerializerOptions { WriteIndented = true });
            Vm.Shell.ShowToast("success", "Backup generado");
        }
        catch (Exception ex)
        {
            Vm.Shell.ShowToast("error", $"Error: {ex.Message}");
        }
    }

    private async void RestoreBackupClick(object? sender, RoutedEventArgs e)
    {
        if (Vm == null || Owner == null) return;
        try
        {
            var files = await Owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                AllowMultiple = false,
                FileTypeFilter = new[] { new FilePickerFileType("JSON") { Patterns = new[] { "*.json" } } },
            });
            if (files.Count == 0) return;

            await using var stream = await files[0].OpenReadAsync();
            var backup = await JsonSerializer.DeserializeAsync<RoleBackup>(stream);
            if (backup == null) throw new Exception("Archivo de backup invalido");

            var confirm = new ConfirmDialogWindow("Restaurar Backup de Roles",
                $"Se restauraran los roles de {backup.MemberRoles.Count} usuarios guardados el {backup.CreatedAt}.",
                "danger", "Restaurar", "Cancelar", true);
            var confirmed = await confirm.ShowDialog<bool>(Owner);
            if (!confirmed) return;

            var result = await Vm.Shell.Discord.RestoreRoleBackupAsync(backup);
            Vm.Shell.ShowToast("success", $"Backup restaurado ({result.FailedCount} fallos)");
            await Vm.LoadRolesAsync();
        }
        catch (Exception ex)
        {
            Vm.Shell.ShowToast("error", $"Error: {ex.Message}");
        }
    }
}
