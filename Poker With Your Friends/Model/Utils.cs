using System;
using System.Collections.Generic;
using System.Text;

namespace Poker_With_Your_Friends.Model
{
    public class Utils
    {
        public static int GetFirstNonNumberIndex(string input)
        {
            if (string.IsNullOrEmpty(input)) return -1;

            for (int i = 0; i < input.Length; i++)
            {
                if (!char.IsDigit(input[i]))
                {
                    return i; // Found it!
                }
            }
            return -1;
        }
    }
}
