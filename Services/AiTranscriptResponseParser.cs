using System.Text.Json;
using JapaneseASR.Models;

namespace JapaneseASR.Services;

public static class AiTranscriptResponseParser
{
    public static string Parse(AiProvider provider, string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (provider == AiProvider.Ollama)
        {
            if (root.TryGetProperty("message", out var message)
                && message.TryGetProperty("content", out var content))
            {
                return content.GetString()?.Trim() ?? string.Empty;
            }
        }
        else
        {
            if (root.TryGetProperty("choices", out var choices)
                && choices.ValueKind == JsonValueKind.Array
                && choices.GetArrayLength() > 0
                && choices[0].TryGetProperty("message", out var message)
                && message.TryGetProperty("content", out var content))
            {
                return content.GetString()?.Trim() ?? string.Empty;
            }
        }

        throw new UserVisibleException("AI API trả về dữ liệu không đọc được.");
    }
}
