using System.Diagnostics;
using System.Text;

namespace JapaneseASR.Services;

public sealed class ProcessRunner
{
    private readonly LogService _logService;

    public ProcessRunner(LogService logService)
    {
        _logService = logService;
    }

    public async Task<ProcessResult> RunAsync(
        string fileName,
        IEnumerable<string> arguments,
        Action<string> onOutput,
        CancellationToken cancellationToken)
    {
        var output = new StringBuilder();
        var error = new StringBuilder();

        using var process = new Process();
        process.StartInfo.FileName = fileName;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.CreateNoWindow = true;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.StandardOutputEncoding = Encoding.UTF8;
        process.StartInfo.StandardErrorEncoding = Encoding.UTF8;

        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        await _logService.WriteAsync($"START {fileName} {string.Join(' ', process.StartInfo.ArgumentList.Cast<string>())}", cancellationToken);

        process.OutputDataReceived += (_, args) =>
        {
            if (args.Data is null)
            {
                return;
            }

            output.AppendLine(args.Data);
            onOutput(args.Data);
        };

        process.ErrorDataReceived += (_, args) =>
        {
            if (args.Data is null)
            {
                return;
            }

            error.AppendLine(args.Data);
            onOutput(args.Data);
        };

        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException("Không thể khởi chạy tiến trình.");
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(cancellationToken);
            var result = new ProcessResult(process.ExitCode, output.ToString(), error.ToString());
            await _logService.WriteAsync($"EXIT {fileName} code={result.ExitCode}", CancellationToken.None);
            return result;
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            await _logService.WriteAsync($"CANCEL {fileName}", CancellationToken.None);
            throw;
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // The process may already be gone; cancellation cleanup should not mask the original action.
        }
    }
}
