using JapaneseASR.Models;

namespace JapaneseASR.Services;

public sealed record AsrJobRequest(
    string InputPath,
    string OutputDirectory,
    TranscriptionMode Mode,
    AiRefinementOptions AiOptions);
