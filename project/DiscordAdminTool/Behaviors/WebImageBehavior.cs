using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;

namespace DiscordAdminTool.Behaviors;

public static class WebImageBehavior
{
    private static readonly HttpClient Http = new();
    private static readonly ConcurrentDictionary<string, Bitmap> Cache = new();

    public static readonly AttachedProperty<string?> UrlProperty =
        AvaloniaProperty.RegisterAttached<Image, string?>("Url", typeof(WebImageBehavior));

    static WebImageBehavior()
    {
        UrlProperty.Changed.AddClassHandler<Image>(OnUrlChanged);
    }

    public static void SetUrl(Image element, string? value) => element.SetValue(UrlProperty, value);
    public static string? GetUrl(Image element) => element.GetValue(UrlProperty);

    private static async void OnUrlChanged(Image image, AvaloniaPropertyChangedEventArgs e)
    {
        var url = e.NewValue as string;
        image.Source = null;
        if (string.IsNullOrWhiteSpace(url)) return;

        if (Cache.TryGetValue(url, out var cached))
        {
            image.Source = cached;
            return;
        }

        try
        {
            var bytes = await Http.GetByteArrayAsync(url);
            await using var ms = new MemoryStream(bytes);
            var bitmap = new Bitmap(ms);
            Cache[url] = bitmap;
            if (GetUrl(image) == url) image.Source = bitmap;
        }
        catch
        {
            // imagen no disponible — se deja el placeholder
        }
    }
}
