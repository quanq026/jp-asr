using System.IO;
using System.Text;

namespace JapaneseASR.Services;

public sealed class LogService
{
    private readonly string _logPath;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public LogService(PortablePaths paths)
    {
        Directory.CreateDirectory(paths.LogsDirectory);
        _logPath = Path.Combine(paths.LogsDirectory, $"asr-{DateTime.Now:yyyyMMdd-HHmmss}.log");
    }

    public string LogPath => _logPath;

    public async Task WriteAsync(string message, CancellationToken cancellationToken = default)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";
        await _lock.WaitAsync(cancellationToken);
        try
        {
            await File.AppendAllTextAsync(_logPath, line, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }
}
