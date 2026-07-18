using Microsoft.UI.Xaml;

namespace Poker_With_Your_Friends.Model;
public class Utils
{
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