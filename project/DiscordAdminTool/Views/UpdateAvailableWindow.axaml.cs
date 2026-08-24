using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using DiscordAdminTool.Services;

namespace DiscordAdminTool.Views;

public partial class UpdateAvailableWindow : Window
{
    public UpdateAvailableWindow()
    {
        InitializeComponent();
    }

    public UpdateAvailableWindow(string version) : this()
    {
        VersionText.Text = $"Version {version} lista para instalar. La app se reinicia sola al terminar.";
    }

    private void LaterClick(object? sender, RoutedEventArgs e) => Close();

    private async void UpdateClick(object? sender, RoutedEventArgs e)
    {
        UpdateButton.IsEnabled = false;
        LaterButton.IsEnabled = false;
        UpdateButton.Content = "Descargando...";
        ErrorText.IsVisible = false;

        try
        {
            await UpdateService.DownloadAndApplyAsync();
        }
        catch (Exception ex)
        {
            ErrorLogger.Log("update-apply", ex);
            ErrorText.Text = "No se pudo actualizar (revisa tu conexion y vuelve a intentar mas tarde).";
            ErrorText.IsVisible = true;
            UpdateButton.Content = "Actualizar";
            UpdateButton.IsEnabled = true;
            LaterButton.IsEnabled = true;
        }
    }
}
