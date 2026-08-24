using Avalonia.Controls;
using Avalonia.Interactivity;
using DiscordAdminTool.ViewModels;

namespace DiscordAdminTool.Views;

public partial class UserManagerView : UserControl
{
    private UserManagerViewModel? Vm => DataContext as UserManagerViewModel;

    public UserManagerView()
    {
        InitializeComponent();
    }

    private void FiltersClick(object? sender, RoutedEventArgs e) => FilterPanel.IsVisible = !FilterPanel.IsVisible;

    private async void OpenRolesPopupClick(object? sender, RoutedEventArgs e)
    {
        if (Vm == null || Vm.SelectedMembers.Count == 0) return;
        var window = TopLevel.GetTopLevel(this) as Window;
        var popup = new RolesPopupWindow(Vm.Shell, Vm.SelectedMembers);
        var changed = await popup.ShowDialog<bool>(window!);
        if (changed) await Vm.LoadAsync();
    }

    private async void OpenMemberPopupClick(object? sender, RoutedEventArgs e)
    {
        if (Vm == null || Vm.SelectedIds.Count == 0) return;
        var window = TopLevel.GetTopLevel(this) as Window;
        var popup = new MemberActionPopupWindow(Vm.Shell, Vm.SelectedIds);
        var changed = await popup.ShowDialog<bool>(window!);
        if (changed)
        {
            Vm.ClearSelection();
            await Vm.LoadAsync();
        }
    }
}
