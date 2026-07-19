using System.Text;

namespace Aman.Shared.Licensing;

/// <summary>
/// Crockford-style Base32 (no padding, unambiguous alphabet — excludes
/// I, L, O, U to avoid confusion with 1/0/V when a user types a code by
/// hand). Used for Machine IDs and activation codes.
/// </summary>
public static class Base32
{
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    public static string Encode(ReadOnlySpan<byte> data)
    {
        var sb = new StringBuilder((data.Length * 8 + 4) / 5);
        int bitBuffer = 0, bitsInBuffer = 0;

        foreach (byte b in data)
        {
            bitBuffer = (bitBuffer << 8) | b;
            bitsInBuffer += 8;
            while (bitsInBuffer >= 5)
            {
                bitsInBuffer -= 5;
                int index = (bitBuffer >> bitsInBuffer) & 0x1F;
                sb.Append(Alphabet[index]);
            }
        }

        if (bitsInBuffer > 0)
        {
            int index = (bitBuffer << (5 - bitsInBuffer)) & 0x1F;
            sb.Append(Alphabet[index]);
        }

        return sb.ToString();
    }

    public static byte[] Decode(string text)
    {
        text = text.Trim().ToUpperInvariant().Replace("-", "").Replace(" ", "");
        var bytes = new List<byte>(text.Length * 5 / 8);
        int bitBuffer = 0, bitsInBuffer = 0;

        foreach (char c in text)
        {
            int index = Alphabet.IndexOf(c);
            if (index < 0)
                throw new FormatException($"Invalid Base32 character '{c}'.");

            bitBuffer = (bitBuffer << 5) | index;
            bitsInBuffer += 5;
            if (bitsInBuffer >= 8)
            {
                bitsInBuffer -= 8;
                bytes.Add((byte)((bitBuffer >> bitsInBuffer) & 0xFF));
            }
        }

        return bytes.ToArray();
    }

    /// <summary>Formats a code into human-friendly groups, e.g. "ABCD-EFGH-JKMN".</summary>
    public static string Group(string code, int groupSize = 4, char separator = '-')
    {
        var sb = new StringBuilder();
        for (int i = 0; i < code.Length; i++)
        {
            if (i > 0 && i % groupSize == 0) sb.Append(separator);
            sb.Append(code[i]);
        }
        return sb.ToString();
    }
}
