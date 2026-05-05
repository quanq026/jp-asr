using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace JapaneseASR.Services;

public static partial class TextPostProcessor
{
    public static async Task RewritePlainTextAsync(string txtPath, CancellationToken cancellationToken)
    {
        if (!File.Exists(txtPath))
        {
            throw new UserVisibleException($"Whisper không tạo file TXT: {Path.GetFileName(txtPath)}. Có thể file nguồn có vấn đề hoặc tên file chứa ký tự đặc biệt.");
        }

        var raw = await File.ReadAllTextAsync(txtPath, Encoding.UTF8, cancellationToken);
        var normalized = Normalize(raw);
        await File.WriteAllTextAsync(txtPath, normalized, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), cancellationToken);
    }

    public static string Normalize(string text)
    {
        var lines = text
            .Split(["\r\n", "\n"], StringSplitOptions.None)
            .Select(line => TimestampPrefixRegex().Replace(line, string.Empty).Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line));

        var joined = string.Join(string.Empty, lines);
        joined = MultiSpaceRegex().Replace(joined, " ");
        joined = SpaceBeforeJapanesePunctuationRegex().Replace(joined, "$1");
        joined = SpaceAfterJapaneseOpenPunctuationRegex().Replace(joined, "$1");

        return joined.Trim() + Environment.NewLine;
    }

    [GeneratedRegex(@"^\[[0-9:.>\-\s]+\]\s*")]
    private static partial Regex TimestampPrefixRegex();

    [GeneratedRegex(@"[ \t]{2,}")]
    private static partial Regex MultiSpaceRegex();

    [GeneratedRegex(@"\s+([、。！？!?）」』】])")]
    private static partial Regex SpaceBeforeJapanesePunctuationRegex();

    [GeneratedRegex(@"([（「『【])\s+")]
    private static partial Regex SpaceAfterJapaneseOpenPunctuationRegex();
}
