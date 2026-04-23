using System.IO;

namespace NymphsCoreManager.Models;

public sealed class DriveChoice
{
    public DriveChoice(string rootPath, long freeBytes)
    {
        RootPath = rootPath;
        FreeBytes = freeBytes;
    }

    public string RootPath { get; }

    public long FreeBytes { get; }

    public long FreeGiB => FreeBytes / (1024L * 1024L * 1024L);

    public string InstallPath => Path.Combine(RootPath, "WSL", "NymphsCore");

    public string DisplayLabel => $"{RootPath} ({FreeGiB} GB free)";
}
