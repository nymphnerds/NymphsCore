using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace NymphsCoreManager.Services;

public sealed class ProcessRunner
{
    private static readonly Regex AnsiSequenceRegex = new(@"\x1B(?:[@-Z\\-_]|\[[0-?]*[ -/]*[@-~])", RegexOptions.Compiled);

    private static string FormatOutput(string value, bool isErrorStream)
    {
        var sanitized = AnsiSequenceRegex.Replace(value.Replace("\0", string.Empty), string.Empty).TrimEnd();
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

        if (!process.Start())
        {
            throw new InvalidOperationException($"Failed to start process: {fileName}");
        }

        var stdoutTask = ReadStreamAsync(process.StandardOutput, isErrorStream: false, outputBuilder, progress, cancellationToken);
        var stderrTask = ReadStreamAsync(process.StandardError, isErrorStream: true, outputBuilder, progress, cancellationToken);

        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }

        await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);

        return new CommandResult(process.ExitCode, outputBuilder.ToString());
    }

    private static async Task ReadStreamAsync(
        StreamReader reader,
        bool isErrorStream,
        StringBuilder outputBuilder,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var buffer = new char[1024];
        var pending = new StringBuilder();
        var lastDelimiterWasCarriageReturn = false;

        while (true)
        {
            var read = await reader.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            for (var i = 0; i < read; i++)
            {
                var current = buffer[i];
                if (current is '\r' or '\n')
                {
                    if (current == '\n' && lastDelimiterWasCarriageReturn)
                    {
                        lastDelimiterWasCarriageReturn = false;
                        continue;
                    }

                    FlushPending(pending, isErrorStream, outputBuilder, progress);
                    lastDelimiterWasCarriageReturn = current == '\r';
                    continue;
                }

                lastDelimiterWasCarriageReturn = false;
                pending.Append(current);
            }
        }

        FlushPending(pending, isErrorStream, outputBuilder, progress);
    }

    private static void FlushPending(
        StringBuilder pending,
        bool isErrorStream,
        StringBuilder outputBuilder,
        IProgress<string>? progress)
    {
        if (pending.Length == 0)
        {
            return;
        }

        var formatted = FormatOutput(pending.ToString(), isErrorStream);
        pending.Clear();
        if (string.IsNullOrWhiteSpace(formatted))
        {
            return;
        }

        outputBuilder.AppendLine(formatted);
        progress?.Report(formatted);
    }
}
