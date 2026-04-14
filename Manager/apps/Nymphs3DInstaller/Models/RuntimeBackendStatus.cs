namespace Nymphs3DInstaller.Models;

public sealed record RuntimeBackendStatus(
    string BackendId,
    string DisplayName,
    bool EnvironmentReady,
    bool ModelsReady,
    bool TestReady,
    string Detail)
{
    public string ReadinessLabel =>
        TestReady
            ? "Ready To Test"
            : EnvironmentReady && !ModelsReady
                ? "Models Missing"
                : !EnvironmentReady
                    ? "Runtime Missing"
                    : "Needs Attention";

    public static RuntimeBackendStatus Unknown(string backendId, string displayName, string detail) =>
        new(backendId, displayName, EnvironmentReady: false, ModelsReady: false, TestReady: false, Detail: detail);
}
