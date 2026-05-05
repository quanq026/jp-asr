using System.IO;
using System.Text;

namespace JapaneseASR.Services;

public static class OutputNameResolver
{
    public static string ResolveUniquePrefix(string outputDirectory, string inputPath)
    {
        Directory.CreateDirectory(outputDirectory);

        var baseName = SanitizeFileName(Path.GetFileNameWithoutExtension(inputPath));

        if (string.IsNullOrWhiteSpace(baseName))
        {
            baseName = "ket-qua";
        }

        var prefix = Path.Combine(outputDirectory, baseName);
        if (!AnyOutputExists(prefix))
        {
            return prefix;
        }

        for (var index = 1; index < 10_000; index++)
        {
            var candidate = Path.Combine(outputDirectory, $"{baseName}-{index}");
            if (!AnyOutputExists(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Không thể tạo tên file kết quả mới.");
    }

    /// <summary>
    /// Sanitizes a filename to only contain safe ASCII characters.
    /// External tools like ffmpeg and whisper-cli (C/C++) may not handle
    /// non-ASCII paths correctly on Windows through their narrow-char APIs.
    /// </summary>
    internal static string SanitizeFileName(string raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return raw;
        }

        var invalid = Path.GetInvalidFileNameChars();
        // Characters that may be misinterpreted by external CLI tools (ffmpeg, whisper-cli)
        var cliUnsafe = new[] { '[', ']', '(', ')', '{', '}', '<', '>', ';', '&', '|', '!', '$', '`', '\'', '"', '#', '%', '^', '~' };
        var sb = new StringBuilder(raw.Length);

        foreach (var c in raw)
        {
            if (c == ' ')
            {
                sb.Append('_');
            }
            else if (c <= 127 && !invalid.Contains(c) && !cliUnsafe.Contains(c))
            {
                sb.Append(c);
            }
            else if (invalid.Contains(c) || cliUnsafe.Contains(c))
            {
                sb.Append('_');
            }
            // Non-ASCII chars are dropped entirely
        }

        var result = sb.ToString();

        // Collapse consecutive underscores
        while (result.Contains("__"))
        {
            result = result.Replace("__", "_");
        }

        // Trim leading/trailing underscores
        result = result.Trim('_');

        // Trim leading/trailing dots (not allowed at start/end of Windows filenames)
        result = result.Trim('.');

        return result;
    }

    private static bool AnyOutputExists(string prefix)
    {
        return File.Exists(prefix + ".txt")
            || File.Exists(prefix + ".srt")
            || File.Exists(prefix + ".vtt");
    }
}
