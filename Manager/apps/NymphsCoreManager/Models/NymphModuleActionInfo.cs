namespace NymphsCoreManager.Models;

public sealed record NymphModuleActionInfo(
    string Id,
    string Label,
    string EntryPoint,
    string ResultMode)
{
    public string DisplayLabel => string.IsNullOrWhiteSpace(Label) ? Id : Label;

    public string ActionName => string.IsNullOrWhiteSpace(EntryPoint) ? Id : EntryPoint;
}
