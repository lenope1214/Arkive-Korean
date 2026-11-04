using System.Buffers.Binary;
using System.Text;

namespace Arkive.Core.GameData;

/// <summary>A text value inside a .loa file, with the byte offset of its length prefix.</summary>
public sealed record LoaString(int Offset, string Text);

/// <summary>
/// Lists the text inside the client's .loa record files (IconInfo, UISoundTheme, ActionCategory, MinimapData...).
/// They share one shape: a few numbers, then records whose text fields are an int32 byte length followed by the
/// bytes and a NUL, with fixed-width numbers between them. A typed reader differs for every file, but the strings
/// alone show what a file holds and what it refers to. The text is CP949 (Korean Windows), unlike GameMsg, which is
/// UTF-8; ASCII-only strings read the same either way.
/// </summary>
public static class LoaStringReader
{
    public const int MaximumLength = 512;
    private static readonly Encoding Korean = CreateKorean();

    /// <summary>
    /// Every plausible length-prefixed string, in file order. It is a scan, not a typed read: numbers can look like
    /// strings, so empty strings (an int32 1 then a zero byte is far more often a number) and byte runs that are not
    /// valid CP949 are left out; on the Korean client's data3.lpk that drops 6.4 million empty and 1,748 binary matches.
    /// </summary>
    public static IReadOnlyList<LoaString> Read(ReadOnlySpan<byte> data)
    {
        var strings = new List<LoaString>();
        var at = 0;
        while (at + 4 <= data.Length)
        {
            var length = BinaryPrimitives.ReadInt32LittleEndian(data[at..]);
            if (length is >= 2 and <= MaximumLength && at + 4 + length <= data.Length &&
                data[at + 4 + length - 1] == 0 && IsText(data.Slice(at + 4, length - 1)) &&
                TryDecode(data.Slice(at + 4, length - 1), out var text))
            {
                strings.Add(new(at, text));
                at += 4 + length;
                continue;
            }
            at++;
        }
        return strings;
    }

    // Text bytes: no NUL inside and no control characters other than tab.
    private static bool IsText(ReadOnlySpan<byte> body)
    {
        foreach (var value in body)
            if (value < 32 && value != 9) return false;
        return true;
    }

    private static bool TryDecode(ReadOnlySpan<byte> body, out string text)
    {
        try
        {
            text = Korean.GetString(body);
            return true;
        }
        catch (DecoderFallbackException)
        {
            text = "";
            return false;
        }
    }

    private static Encoding CreateKorean()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(949, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
    }
}
