namespace JapaneseASR.Services;

public static class AiTranscriptChunker
{
    private static readonly char[] SentenceEndings = ['。', '？', '！', '?', '!'];

    public static IReadOnlyList<string> Split(string text, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        if (maxLength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxLength));
        }

        var chunks = new List<string>();
        var start = 0;
        while (start < text.Length)
        {
            var length = Math.Min(maxLength, text.Length - start);
            if (start + length < text.Length)
            {
                var window = text.AsSpan(start, length);
                var splitAt = window.LastIndexOfAny(SentenceEndings);
                if (splitAt >= Math.Min(300, maxLength / 3))
                {
                    length = splitAt + 1;
                }
            }

            var chunk = text.Substring(start, length).Trim();
            if (!string.IsNullOrWhiteSpace(chunk))
            {
                chunks.Add(chunk);
            }

            start += length;
        }

        return chunks;
    }
}
