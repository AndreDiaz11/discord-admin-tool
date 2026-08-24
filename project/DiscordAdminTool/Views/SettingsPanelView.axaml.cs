using Avalonia.Controls;
using Avalonia.Interactivity;
using DiscordAdminTool.ViewModels;

namespace DiscordAdminTool.Views;

public partial class SettingsPanelView : UserControl
{
    private SettingsPanelViewModel? Vm => DataContext as SettingsPanelViewModel;

    public SettingsPanelView()
    {
        InitializeComponent();
    }

    private async void ClearTokenClick(object? sender, RoutedEventArgs e)
    {
        if (Vm == null) return;
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner == null) return;

        var confirm = new ConfirmDialogWindow("Olvidar token",
            "Tendras que volver a conectar el bot con un token la proxima vez que abras la app.",
            "danger", "Olvidar", "Cancelar", false);
        var confirmed = await confirm.ShowDialog<bool>(owner);
        if (!confirmed) return;
        await Vm.ClearTokenAsync();
    }
}
