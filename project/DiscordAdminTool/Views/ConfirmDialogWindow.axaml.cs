using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace DiscordAdminTool.Views;

public partial class ConfirmDialogWindow : Window
{
    private bool _requireTypedConfirm;

    public ConfirmDialogWindow()
    {
        InitializeComponent();
    }

    public ConfirmDialogWindow(string title, string message, string variant = "info", string confirmLabel = "Confirmar",
        string cancelLabel = "Cancelar", bool requireTypedConfirm = false) : this()
    {
        TitleText.Text = title;
        MessageText.Text = message;
        CancelButton.Content = cancelLabel;
        ConfirmButton.Content = confirmLabel;
        _requireTypedConfirm = requireTypedConfirm;

        var (accent, icon) = variant switch
        {
            "danger" => (Color.Parse("#ED4245"), "⚠"),
            "warning" => (Color.Parse("#F9A825"), "⚠"),
            "success" => (Color.Parse("#3BA55D"), "✓"),
            _ => (Color.Parse("#5865F2"), "ℹ"),
        };

        IconBadge.Background = new SolidColorBrush(accent, 0.18);
        IconText.Text = icon;
        IconText.Foreground = new SolidColorBrush(accent);
        ConfirmButton.Background = new SolidColorBrush(accent);

        if (requireTypedConfirm)
        {
            TypedConfirmPanel.IsVisible = true;
            ConfirmButton.IsEnabled = false;
            TypedConfirmInput.PropertyChanged += (_, e) =>
            {
                if (e.Property.Name != "Text") return;
                ConfirmButton.IsEnabled = TypedConfirmInput.Text?.Trim() == "CONFIRMAR";
            };
        }
    }

    private void CancelClick(object? sender, RoutedEventArgs e) => Close(false);

    private void ConfirmClick(object? sender, RoutedEventArgs e) => Close(true);
}
