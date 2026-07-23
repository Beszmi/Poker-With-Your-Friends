using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Poker_With_Your_Friends.Model;
public class Utils
{
    public static ImageSource PathToImage(string path)
    {
        string fullPath;
        if (string.IsNullOrEmpty(path))
        {
            fullPath = Path.Combine(Game.PFPfilePath, "Emptypfp.jpg");
        }
        else if (Path.IsPathRooted(path))
        {
            fullPath = path;
        }
        else
        {
            string relativePath = path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            fullPath = Path.Combine(AppContext.BaseDirectory, relativePath);
        }

        if (!File.Exists(fullPath))
        {
            fullPath = Path.Combine(Game.PFPfilePath, "Emptypfp.jpg");
        }

        return new BitmapImage(new Uri(fullPath, UriKind.Absolute));
    }

    public static int GetFirstNonNumberIndex(string input)
    {
        if (string.IsNullOrEmpty(input)) return -1;

        for (int i = 0; i < input.Length; i++)
        {
            if (!char.IsDigit(input[i]))
            {
                return i;
            }
        }
        return -1;
    }

    public static Visibility BoolToVis(bool value)
    {
        return value ? Visibility.Visible : Visibility.Collapsed;
    }

    public static Visibility AntiBoolToVis(bool value)
    {
        return value ? Visibility.Collapsed : Visibility.Visible;
    }

    // Basic safety checks (NOT COMPLETLY SAFE!)
    public static bool HasJpegSignature(string path)
    {
        using var fs = File.OpenRead(path);
        if (fs.Length < 4) return false;

        Span<byte> header = stackalloc byte[3];
        fs.ReadExactly(header);

        return header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
    }

    public static async Task<bool> IsValidJpegAsync(StorageFile file)
    {
        try
        {
            using IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.Read);
            BitmapDecoder decoder = await BitmapDecoder.CreateAsync(BitmapDecoder.JpegDecoderId, stream);

            PixelDataProvider pixelData = await decoder.GetPixelDataAsync();
            byte[] pixels = pixelData.DetachPixelData();

            return pixels.Length > 0 && decoder.PixelWidth > 0 && decoder.PixelHeight > 0;
        }
        catch
        {
            return false;
        }
    }

    public static long? GetTrailingByteCount(string path)
    {
        byte[] data = File.ReadAllBytes(path);
        if (data.Length < 4 || data[0] != 0xFF || data[1] != 0xD8) return null;

        int i = 2;
        while (i < data.Length - 1)
        {
            if (data[i] == 0xFF && data[i + 1] == 0xD9)
            {
                long trailing = data.Length - (i + 2);
                return trailing;
            }
            i++;
        }

        return null;
    }

    public static async Task<bool> IsSafeJpegAsync(StorageFile file)
    {
        string path = file.Path;

        if (!HasJpegSignature(path)) return false;
        if (!await IsValidJpegAsync(file)) return false;

        var trailing = GetTrailingByteCount(path);
        if (trailing is > 1024) return false;

        return true;
    }


}