namespace NymphsCoreManager.Models;

public sealed class WslConfigModeOption
{
    public WslConfigModeOption(WslConfigMode mode, string title, string description)
    {
        Mode = mode;
        Title = title;
        Description = description;
    }

    public WslConfigMode Mode { get; }

    public string Title { get; }

    public string Description { get; }
}
