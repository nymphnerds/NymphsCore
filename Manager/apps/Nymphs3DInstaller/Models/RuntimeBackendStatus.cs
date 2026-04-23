namespace Nymphs3DInstaller.Models;

public sealed record RuntimeBackendStatus(
    string BackendId,
    string DisplayName,
    bool EnvironmentReady,
    bool ModelsReady,
    bool TestReady,
    string Detail)
{
    public bool IsNotChecked =>
        !EnvironmentReady
        && !ModelsReady
        && !TestReady
        && Detail.Contains("Open Runtime Tools", StringComparison.OrdinalIgnoreCase);

    public string ReadinessLabel =>
        IsNotChecked
            ? "Not Checked"
            : TestReady
            ? "Ready To Test"
            : EnvironmentReady && !ModelsReady
                ? "Models Missing"
                : !EnvironmentReady
                    ? "Runtime Missing"
                    : "Needs Attention";

    public string CompactDetail =>
        Detail.Length <= 220
            ? Detail
            : string.Concat(Detail.AsSpan(0, 217), "...");

    public static RuntimeBackendStatus Unknown(string backendId, string displayName, string detail) =>
        new(backendId, displayName, EnvironmentReady: false, ModelsReady: false, TestReady: false, Detail: detail);
}
