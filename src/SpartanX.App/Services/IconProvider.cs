using Microsoft.UI.Xaml.Media.Imaging;
using SpartanX.Core.Abstractions;
using Windows.Storage;
using Windows.Storage.FileProperties;

namespace SpartanX.App.Services;

/// <summary>Loads high-resolution shell icons (the same ones Explorer shows) as WinUI images.</summary>
internal sealed class IconProvider : IIconProvider
{
    private readonly Dictionary<string, BitmapImage> _cache = new(StringComparer.OrdinalIgnoreCase);

    /// <remarks>Must be called on the UI thread (BitmapImage is a DependencyObject).</remarks>
    public async Task<object?> GetIconAsync(string path, int size)
    {
        var key = $"{path}|{size}";
        if (_cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        StorageItemThumbnail? thumbnail;
        if (Directory.Exists(path))
        {
            var folder = await StorageFolder.GetFolderFromPathAsync(path);
            thumbnail = await folder.GetThumbnailAsync(ThumbnailMode.SingleItem, (uint)size, ThumbnailOptions.UseCurrentScale);
        }
        else if (File.Exists(path))
        {
            var file = await StorageFile.GetFileFromPathAsync(path);
            thumbnail = await file.GetThumbnailAsync(ThumbnailMode.SingleItem, (uint)size, ThumbnailOptions.UseCurrentScale);
        }
        else
        {
            return null;
        }

        if (thumbnail is null)
        {
            return null;
        }

        using (thumbnail)
        {
            var image = new BitmapImage();
            await image.SetSourceAsync(thumbnail);
            _cache[key] = image;
            return image;
        }
    }
}
