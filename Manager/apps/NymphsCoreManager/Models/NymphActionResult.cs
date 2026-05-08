namespace NymphsCoreManager.Models;

public sealed record NymphActionResult
{
    public NymphActionKind ActionKind { get; init; }
    public string ModuleId { get; init; }
    public bool Success { get; init; }
    public int ExitCode { get; init; }
    public string Output { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTimeOffset Timestamp { get; init; }

    public NymphActionResult(NymphActionKind actionKind, string moduleId)
    {
        ActionKind = actionKind;
        ModuleId = moduleId;
        Timestamp = DateTimeOffset.UtcNow;
        Output = string.Empty;
    }

    public static NymphActionResult SuccessResult(NymphActionKind actionKind, string moduleId, string output)
        => new(actionKind, moduleId)
        {
            Success = true,
            ExitCode = 0,
            Output = output
        };

    public static NymphActionResult FailureResult(NymphActionKind actionKind, string moduleId, string errorMessage, string output = "", int exitCode = -1)
        => new(actionKind, moduleId)
        {
            Success = false,
            ExitCode = exitCode,
            Output = output,
            ErrorMessage = errorMessage
        };

    public static NymphActionResult NotImplementedResult(NymphActionKind actionKind, string moduleId, string reason)
        => new(actionKind, moduleId)
        {
            Success = false,
            ExitCode = -1,
            ErrorMessage = $"Action not implemented: {reason}"
        };
}