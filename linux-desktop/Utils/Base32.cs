using System.Collections.Generic;
using System.Text;

namespace TwoFactorAuth.Utils;

public static class Base32
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static byte[] Decode(string input)
    {
        var sanitized = input.Replace(" ", "").Replace("-", "").Trim('=').ToUpperInvariant();
        var bytes = new List<byte>();
        int buffer = 0;
        int bitsLeft = 0;
        foreach (var c in sanitized)
        {
            int val = Alphabet.IndexOf(c);
            if (val < 0) continue;
            buffer = (buffer << 5) | val;
            bitsLeft += 5;
            if (bitsLeft >= 8)
            {
                bitsLeft -= 8;
                bytes.Add((byte)((buffer >> bitsLeft) & 0xFF));
            }
        }
        return bytes.ToArray();
    }

    public static string Encode(byte[] data)
    {
        if (data == null || data.Length == 0)
            return string.Empty;

        var result = new StringBuilder();
        int buffer = 0;
        int bitsLeft = 0;

        foreach (var b in data)
        {
            buffer = (buffer << 8) | b;
            bitsLeft += 8;

            while (bitsLeft >= 5)
            {
                bitsLeft -= 5;
                int index = (buffer >> bitsLeft) & 0x1F;
                result.Append(Alphabet[index]);
            }
        }

        if (bitsLeft > 0)
        {
            int index = (buffer << (5 - bitsLeft)) & 0x1F;
            result.Append(Alphabet[index]);
        }

        while (result.Length % 8 != 0)
        {
            result.Append('=');
        }

        return result.ToString();
    }
}
