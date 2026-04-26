namespace NymphsCoreManager.Models;

public sealed class TrellisGgufQuantOption
{
    public TrellisGgufQuantOption(string value, string title, string description)
    {
        Value = value;
        Title = title;
        Description = description;
    }

    public string Value { get; }

    public string Title { get; }

    public string Description { get; }
}
