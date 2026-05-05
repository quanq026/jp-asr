using System.Text.Json;
using System.Text.Encodings.Web;
using JapaneseASR.Models;

namespace JapaneseASR.Services;

public static class AiTranscriptRequestFactory
{
    public static AiTranscriptRequest Create(
        AiProvider provider,
        string model,
        string transcript,
        string glossary)
    {
        var messages = new object[]
        {
            new
            {
                role = "system",
                content = BuildSystemPrompt(glossary)
            },
            new
            {
                role = "user",
                content = transcript
            }
        };

        object body = provider == AiProvider.Ollama
            ? new
            {
                model,
                stream = false,
                messages,
                options = new
                {
                    temperature = 0.1
                }
            }
            : new
            {
                model,
                temperature = 0.1,
                messages
            };

        return new AiTranscriptRequest(JsonSerializer.Serialize(body, new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        }));
    }

    private static string BuildSystemPrompt(string glossary)
    {
        return $"""
        Bạn là công cụ hậu xử lý transcript tiếng Nhật định dạng SRT (SubRip).
        Nhiệm vụ: sửa nhẹ transcript ASR tiếng Nhật trong file SRT để dễ đọc và đúng thuật ngữ hơn.
        Quy tắc bắt buộc:
        - GIỮ NGUYÊN tất cả timestamp (dòng HH:MM:SS,mmm --> HH:MM:SS,mmm) và số thứ tự.
        - CHỈ sửa text tiếng Nhật trong từng dòng subtitle.
        - Không dịch.
        - Không thêm ý mới.
        - Không tóm tắt.
        - Không giải thích.
        - Chỉ sửa lỗi kana/kanji rõ ràng, thuật ngữ, dấu câu, khoảng trắng và ngắt câu.
        - Nếu không chắc một chỗ nào đó, giữ nguyên.
        - Output phải giữ đúng định dạng SRT (số thứ tự, timestamp, text, dòng trống giữa các entry).

        Thuật ngữ ưu tiên:
        {glossary}
        """;
    }
}
