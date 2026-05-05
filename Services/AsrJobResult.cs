namespace JapaneseASR.Services;

public sealed record AsrJobResult(string TextPath, string SrtPath, string VttPath, string? AiTextPath, string LogPath);
