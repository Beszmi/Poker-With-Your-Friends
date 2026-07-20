using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.IO;

namespace Poker_With_Your_Friends.Model;
public class Utils
{
    public static ImageSource PathToImage(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            path = "/Assets/pfp/Emptypfp.jpg";
        }
        string relativePath = path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        string fullPath = Path.Combine(AppContext.BaseDirectory, relativePath);
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
}