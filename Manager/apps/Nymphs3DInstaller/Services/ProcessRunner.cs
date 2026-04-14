using System.Diagnostics;
using System.Text;

namespace Nymphs3DInstaller.Services;

public sealed class ProcessRunner
{
    private static string FormatOutput(string value, bool isErrorStream)
    {
        var sanitized = value.Replace("\0", string.Empty);
        if (!isErrorStream || string.IsNullOrWhiteSpace(sanitized))
        {
            return sanitized;
        }

        if (sanitized.StartsWith("ERROR: pip's dependency resolver", StringComparison.Ordinal))
        {
            return $"[stderr warning] {sanitized}";
        }

        return $"[stderr] {sanitized}";
    }

    public async Task<CommandResult> RunAsync(
        string fileName,
        IEnumerable<string> arguments,
        string workingDirectory,
        IProgress<string>? progress,
        IReadOnlyDictionary<string, string?>? environmentVariables,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        if (environmentVariables is not null)
        {
            foreach (var pair in environmentVariables)
            {
                if (pair.Value is null)
                {
                    startInfo.Environment.Remove(pair.Key);
                }
                else
                {
                    startInfo.Environment[pair.Key] = pair.Value;
                }
            }
        }

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        var outputBuilder = new StringBuilder();
        var stdoutClosed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var stderrClosed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null)
            {
                stdoutClosed.TrySetResult(true);
                return;
            }

            var formatted = FormatOutput(e.Data, isErrorStream: false);
            outputBuilder.AppendLine(formatted);
            progress?.Report(formatted);
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null)
            {
                stderrClosed.TrySetResult(true);
                return;
            }

            var formatted = FormatOutput(e.Data, isErrorStream: true);
            outputBuilder.AppendLine(formatted);
            progress?.Report(formatted);
        };

        if (!process.Start())
        {
            throw new InvalidOperationException($"Failed to start process: {fileName}");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        await Task.WhenAll(stdoutClosed.Task, stderrClosed.Task).ConfigureAwait(false);

        return new CommandResult(process.ExitCode, outputBuilder.ToString());
    }
}
