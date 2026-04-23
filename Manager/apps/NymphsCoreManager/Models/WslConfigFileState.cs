namespace NymphsCoreManager.Models;

public sealed class WslConfigFileState
{
    public WslConfigFileState(string path, bool exists, int? memoryGb, int? processors, int? swapGb)
    {
        Path = path;
        Exists = exists;
        MemoryGb = memoryGb;
        Processors = processors;
        SwapGb = swapGb;
    }

    public string Path { get; }

    public bool Exists { get; }

    public int? MemoryGb { get; }

    public int? Processors { get; }

    public int? SwapGb { get; }
}
