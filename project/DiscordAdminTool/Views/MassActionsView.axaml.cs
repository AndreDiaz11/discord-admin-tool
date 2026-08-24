using Avalonia.Controls;
using Avalonia.Interactivity;
using DiscordAdminTool.ViewModels;

namespace DiscordAdminTool.Views;

public partial class MassActionsView : UserControl
{
    private MassActionsViewModel? Vm => DataContext as MassActionsViewModel;

    public MassActionsView()
    {
        InitializeComponent();
    }

    private async void ExecutePurgeClick(object? sender, RoutedEventArgs e)
    {
        if (Vm == null) return;
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner == null) return;

        var preview = await Vm.PreviewForConfirmAsync();
        if (preview == null) return;

        if (preview.AffectedCount == 0)
        {
            Vm.Shell.ShowToast("info", "No hay usuarios que coincidan con el filtro");
            return;
        }

        var confirm = new ConfirmDialogWindow("Confirmar Purga Masiva",
            $"Vas a expulsar a {preview.AffectedCount} usuarios. Esta accion es irreversible.",
            "danger", "Confirmar", "Cancelar", preview.AffectedCount > 50);
        var confirmed = await confirm.ShowDialog<bool>(owner);
        if (!confirmed) return;

        await Vm.ExecutePurgeAsync();
    }
}
