using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiscordAdminTool.Models;

namespace DiscordAdminTool.ViewModels;

public partial class ChannelRow : ObservableObject
{
    public ChannelInfo Channel { get; }
    public string TypeIcon => Channel.Type switch { ChannelKind.Voice => "🔊", ChannelKind.Announcement => "📢", _ => "#" };
    public string TypeLabel => Channel.Type switch { ChannelKind.Voice => "Voz", ChannelKind.Announcement => "Anuncios", _ => "Texto" };
    public string MessageCountLabel => Channel.Error != null ? "—" : Channel.MessageCountCapped ? $"{Channel.MessageCount}+" : Channel.MessageCount.ToString();
    public string ContentLabel => Channel.HasContent == true ? "Tiene contenido" : Channel.HasContent == false ? "Vacio" : "Desconocido";
    public string ContentVariant => Channel.HasContent == true ? "success" : Channel.HasContent == false ? "warning" : "danger";
    public bool CanClean => Channel.HasContent != false;

    public ChannelRow(ChannelInfo channel) => Channel = channel;
}

public partial class ChannelCleanerViewModel : ViewModelBase
{
    public MainWindowViewModel Shell { get; }
    public ObservableCollection<ChannelRow> Rows { get; } = new();
    [ObservableProperty] private bool _loading;
    [ObservableProperty] private string _statusText = "";

    public ChannelCleanerViewModel(MainWindowViewModel shell)
    {
        Shell = shell;
        _ = LoadAsync();
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        Loading = true;
        StatusText = "Revisando canales (puede tardar unos segundos)...";
        try
        {
            var channels = await Shell.Discord.ListChannelsAsync();
            Rows.Clear();
            foreach (var c in channels) Rows.Add(new ChannelRow(c));
            StatusText = channels.Count == 0 ? "Sin canales de texto o voz" : "";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            Loading = false;
        }
    }
}
