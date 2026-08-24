using Avalonia.Controls;
using Avalonia.Interactivity;
using DiscordAdminTool.ViewModels;

namespace DiscordAdminTool.Views;

public partial class ChannelCleanerView : UserControl
{
    private ChannelCleanerViewModel? Vm => DataContext as ChannelCleanerViewModel;

    public ChannelCleanerView()
    {
        InitializeComponent();
    }

    private async void CleanClick(object? sender, RoutedEventArgs e)
    {
        if (Vm == null || sender is not Button { Tag: ChannelRow row }) return;
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner == null) return;

        var popup = new CleanChannelPopupWindow(Vm.Shell, row.Channel);
        var changed = await popup.ShowDialog<bool>(owner);
        if (changed) await Vm.LoadAsync();
    }
}
