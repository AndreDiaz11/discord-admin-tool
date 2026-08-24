using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Interactivity;
using DiscordAdminTool.Models;

namespace DiscordAdminTool.Views;

public class RoleFormResult
{
    public string Name { get; set; } = "";
    public string? Color { get; set; }
    public bool Hoist { get; set; }
    public bool Mentionable { get; set; }
}

public partial class RoleFormPopupWindow : Window
{
    public RoleFormPopupWindow()
    {
        InitializeComponent();
    }

    public RoleFormPopupWindow(string title, RoleInfo? initial) : this()
    {
        TitleText.Text = title;
        if (initial != null)
        {
            NameBox.Text = initial.Name;
            ColorBox.Text = initial.Color != "#000000" ? initial.Color : "#5865F2";
        }
        else
        {
            ColorBox.Text = "#5865F2";
        }
    }

    private void CancelClick(object? sender, RoutedEventArgs e) => Close(null);

    private void SaveClick(object? sender, RoutedEventArgs e)
    {
        var name = NameBox.Text?.Trim();
        if (string.IsNullOrEmpty(name)) return;

        var color = ColorBox.Text?.Trim();
        if (string.IsNullOrEmpty(color) || !Regex.IsMatch(color, "^#?[0-9a-fA-F]{6}$")) color = "#5865F2";
        if (!color.StartsWith("#")) color = "#" + color;

        Close(new RoleFormResult { Name = name, Color = color, Hoist = HoistCheck.IsChecked == true, Mentionable = MentionableCheck.IsChecked == true });
    }
}
