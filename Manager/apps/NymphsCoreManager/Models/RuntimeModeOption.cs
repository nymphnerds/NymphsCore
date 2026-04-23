namespace NymphsCoreManager.Models;

public sealed class RuntimeModeOption
{
    public RuntimeModeOption(RuntimeSetupMode mode, string title, string description)
    {
        Mode = mode;
        Title = title;
        Description = description;
    }

    public RuntimeSetupMode Mode { get; }

    public string Title { get; }

    public string Description { get; }
}
