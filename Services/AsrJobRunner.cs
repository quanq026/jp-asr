using System.IO;
using System.Text;
using JapaneseASR.Models;

namespace JapaneseASR.Services;

public sealed class AsrJobRunner
{
    private readonly PortablePaths _paths;
    private readonly ProcessRunner _processRunner;
    private readonly LogService _logService;
    private readonly AiTranscriptRefiner _aiTranscriptRefiner;

    public AsrJobRunner(
        PortablePaths paths,
        ProcessRunner processRunner,
        LogService logService,
        AiTranscriptRefiner aiTranscriptRefiner)
    {
        _paths = paths;
        _processRunner = processRunner;
        _logService = logService;
        _aiTranscriptRefiner = aiTranscriptRefiner;
    }

    public async Task<AsrJobResult> RunAsync(
        AsrJobRequest request,
        Action<string> onStatus,
        CancellationToken cancellationToken)
    {
        _paths.EnsureWritableDirectories();
        ValidateRequest(request);

        var jobId = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var tempWavPath = Path.Combine(_paths.TempDirectory, $"asr-{jobId}.wav");
        var outputPrefix = OutputNameResolver.ResolveUniquePrefix(request.OutputDirectory, request.InputPath);

        try
        {
            onStatus("Đang tách audio về WAV mono 16kHz...");
            await ExtractAudioAsync(request.InputPath, tempWavPath, onStatus, cancellationToken);

            onStatus("Đang nhận dạng tiếng Nhật bằng Whisper...");
            await TranscribeAsync(request.Mode, tempWavPath, outputPrefix, onStatus, cancellationToken);

            onStatus("Đang tạo file TXT dễ đọc...");
            var textPath = outputPrefix + ".txt";
            var srtPath = outputPrefix + ".srt";
            var vttPath = outputPrefix + ".vtt";

            await TextPostProcessor.RewritePlainTextAsync(textPath, cancellationToken);
            EnsureOutputExists(srtPath, "SRT");
            EnsureOutputExists(vttPath, "VTT");

            string? aiTextPath = null;
            if (request.AiOptions.Enabled)
            {
                aiTextPath = await RefineSrtWithAiAsync(srtPath, outputPrefix, request.AiOptions, onStatus, cancellationToken);
            }

            onStatus("Hoàn tất.");
            return new AsrJobResult(textPath, srtPath, vttPath, aiTextPath, _logService.LogPath);
        }
        finally
        {
            TryDelete(tempWavPath);
        }
    }

    private void ValidateRequest(AsrJobRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.InputPath) || !File.Exists(request.InputPath))
        {
            throw new UserVisibleException("Không thể đọc file này. Vui lòng chọn lại file audio/video.");
        }

        if (string.IsNullOrWhiteSpace(request.OutputDirectory))
        {
            throw new UserVisibleException("Vui lòng chọn thư mục xuất kết quả.");
        }

        RequireFile(_paths.FfmpegPath, "Thiếu bin\\ffmpeg.exe. Vui lòng dùng lại bản đóng gói đầy đủ.");
        RequireFile(_paths.WhisperPath, "Thiếu bin\\whisper-cli.exe. Vui lòng dùng lại bản đóng gói đầy đủ.");

        _ = GetModelPath(request.Mode);
    }

    private async Task ExtractAudioAsync(
        string inputPath,
        string tempWavPath,
        Action<string> onStatus,
        CancellationToken cancellationToken)
    {
        var arguments = new[]
        {
            "-y",
            "-hide_banner",
            "-i", inputPath,
            "-vn",
            "-ac", "1",
            "-ar", "16000",
            "-f", "wav",
            tempWavPath
        };

        var result = await _processRunner.RunAsync(_paths.FfmpegPath, arguments, line => ForwardInterestingLine(line, onStatus), cancellationToken);
        if (result.ExitCode != 0 || !File.Exists(tempWavPath))
        {
            await _logService.WriteAsync(result.StandardError, CancellationToken.None);
            throw new UserVisibleException("Không thể đọc file này hoặc tách audio thất bại.");
        }
    }

    private async Task TranscribeAsync(
        TranscriptionMode mode,
        string tempWavPath,
        string outputPrefix,
        Action<string> onStatus,
        CancellationToken cancellationToken)
    {
        var arguments = new List<string>
        {
            "-m", GetModelPath(mode),
            "-f", tempWavPath,
            "-l", "ja",
            "-otxt",
            "-osrt",
            "-ovtt",
            "-of", outputPrefix,
            "-t", GetThreadCount().ToString(),
            "-ng"
        };

        if (mode == TranscriptionMode.Balanced)
        {
            arguments.AddRange(["-bs", "1"]);
        }
        else
        {
            arguments.AddRange(["-bs", "3"]);
        }

        var result = await _processRunner.RunAsync(_paths.WhisperPath, arguments, line => ForwardInterestingLine(line, onStatus), cancellationToken);
        if (result.ExitCode != 0)
        {
            await _logService.WriteAsync(result.StandardError, CancellationToken.None);
            throw new UserVisibleException("Whisper xử lý thất bại. Vui lòng thử file khác hoặc chế độ Cân bằng.");
        }
    }

    private async Task<string?> RefineSrtWithAiAsync(
        string srtPath,
        string outputPrefix,
        AiRefinementOptions options,
        Action<string> onStatus,
        CancellationToken cancellationToken)
    {
        try
        {
            onStatus("Đang gửi SRT sang AI để sửa nhẹ...");
            var transcript = await File.ReadAllTextAsync(srtPath, Encoding.UTF8, cancellationToken);
            var refined = await _aiTranscriptRefiner.RefineAsync(options, transcript, cancellationToken, onStatus);
            var aiSrtPath = outputPrefix + ".ai.srt";
            await File.WriteAllTextAsync(aiSrtPath, refined, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), cancellationToken);
            onStatus("Đã tạo file AI SRT.");
            return aiSrtPath;
        }
        catch (UserVisibleException ex)
        {
            await _logService.WriteAsync($"AI skipped: {ex.Message}", CancellationToken.None);
            onStatus($"AI không xử lý được: {ex.Message}");
            onStatus("TXT/SRT/VTT từ Whisper vẫn đã được tạo.");
            return null;
        }
    }

    private string GetModelPath(TranscriptionMode mode)
    {
        if (mode == TranscriptionMode.Accurate)
        {
            RequireFile(_paths.MediumModelPath, $"Thiếu model {Path.GetFileName(_paths.MediumModelPath)}. Vui lòng dùng lại bản đóng gói đầy đủ.");
            return _paths.MediumModelPath;
        }

        var candidates = new[] { _paths.SmallModelPath, _paths.SmallModelFallbackPath, _paths.SmallModelHighQualityPath };
        var modelPath = candidates.FirstOrDefault(File.Exists);
        if (modelPath is not null)
        {
            return modelPath;
        }

        throw new UserVisibleException("Thiếu model small cho chế độ Cân bằng. Vui lòng dùng lại bản đóng gói đầy đủ.");
    }

    private static int GetThreadCount()
    {
        return Math.Clamp(Environment.ProcessorCount, 2, 8);
    }

    private static void RequireFile(string path, string message)
    {
        if (!File.Exists(path))
        {
            throw new UserVisibleException(message);
        }
    }

    private static void EnsureOutputExists(string path, string label)
    {
        if (!File.Exists(path))
        {
            throw new UserVisibleException($"Whisper không tạo file {label}. Vui lòng xem log để kiểm tra lỗi.");
        }
    }

    private static void ForwardInterestingLine(string line, Action<string> onStatus)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        if (line.Contains("error", StringComparison.OrdinalIgnoreCase)
            || line.Contains("failed", StringComparison.OrdinalIgnoreCase)
            || line.Contains("%", StringComparison.OrdinalIgnoreCase)
            || line.Contains("whisper_", StringComparison.OrdinalIgnoreCase))
        {
            onStatus(line);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Temp cleanup should not hide the actual processing result.
        }
    }
}
