namespace NymphsCoreManager.Models;

public sealed class WslConfigValues
{
    public WslConfigValues(int memoryGb, int processors, int swapGb)
    {
        MemoryGb = memoryGb;
        Processors = processors;
        SwapGb = swapGb;
    }

    public int MemoryGb { get; }

    public int Processors { get; }

    public int SwapGb { get; }
}
