using System.IO;

namespace JapaneseASR.Services;

public sealed class PortablePaths
{
    public PortablePaths(string baseDirectory)
    {
        BaseDirectory = baseDirectory;
        BinDirectory = Path.Combine(baseDirectory, "bin");
        ModelsDirectory = Path.Combine(baseDirectory, "models");
        ConfigDirectory = Path.Combine(baseDirectory, "config");
        OutputDirectory = Path.Combine(baseDirectory, "output");
        LogsDirectory = Path.Combine(baseDirectory, "logs");
        TempDirectory = Path.Combine(Path.GetTempPath(), "JapaneseASR");
        FfmpegPath = Path.Combine(BinDirectory, "ffmpeg.exe");
        WhisperPath = Path.Combine(BinDirectory, "whisper-cli.exe");
        SmallModelPath = Path.Combine(ModelsDirectory, "ggml-small-q5_0.bin");
        SmallModelFallbackPath = Path.Combine(ModelsDirectory, "ggml-small-q5_1.bin");
        SmallModelHighQualityPath = Path.Combine(ModelsDirectory, "ggml-small-q8_0.bin");
        MediumModelPath = Path.Combine(ModelsDirectory, "ggml-medium-q5_0.bin");
        AiLocalSettingsPath = Path.Combine(ConfigDirectory, "ai.local.json");
    }

    public string BaseDirectory { get; }
    public string BinDirectory { get; }
    public string ModelsDirectory { get; }
    public string ConfigDirectory { get; }
    public string OutputDirectory { get; }
    public string LogsDirectory { get; }
    public string TempDirectory { get; }
    public string FfmpegPath { get; }
    public string WhisperPath { get; }
    public string SmallModelPath { get; }
    public string SmallModelFallbackPath { get; }
    public string SmallModelHighQualityPath { get; }
    public string MediumModelPath { get; }
    public string AiLocalSettingsPath { get; }

    public void EnsureWritableDirectories()
    {
        Directory.CreateDirectory(OutputDirectory);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(TempDirectory);
        Directory.CreateDirectory(ConfigDirectory);
    }
}
