namespace NymphsCoreManager.Models;

public sealed class SystemCheckItem
{
    public SystemCheckItem(string title, string description, CheckState status, string details, string? key = null)
    {
        Title = title;
        Description = description;
        Status = status;
        Details = details;
        Key = key ?? title;
    }

    public string Title { get; }

    public string Description { get; }

    public string Key { get; }

    public CheckState Status { get; }

    public string Details { get; }

    public string StatusLabel =>
        Status switch
        {
            CheckState.Pass => "Pass",
            CheckState.Warning => "Warning",
            CheckState.Fail => "Fail",
            _ => "Pending",
        };

    public string StatusBadgeBackground =>
        Status switch
        {
            CheckState.Pass => "#235756",
            CheckState.Warning => "#B7791F",
            CheckState.Fail => "#B74322",
            _ => "#6B6259",
        };
}
