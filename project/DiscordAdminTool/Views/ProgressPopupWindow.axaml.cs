using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace DiscordAdminTool.Views;

public partial class ProgressPopupWindow : Window
{
    public event Action? CancelRequested;
    private string _channelName = "";

    public ProgressPopupWindow()
    {
        InitializeComponent();
    }

    public ProgressPopupWindow(string channelName) : this()
    {
        _channelName = channelName;
        TitleText.Text = $"Eliminando mensajes de #{channelName}";
        Update(0, 0, false);
    }

    public void Update(int deleted, int failed, bool cancelling)
    {
        Dispatcher.UIThread.Post(() =>
        {
            BodyText.Text = failed > 0 ? $"{deleted} borrados, {failed} fallidos..." : $"{deleted} borrados...";
            CancelButton.IsEnabled = !cancelling;
            CancelButton.Content = cancelling ? "Cancelando..." : "Cancelar";
        });
    }

    private void CancelClick(object? sender, RoutedEventArgs e) => CancelRequested?.Invoke();
}
