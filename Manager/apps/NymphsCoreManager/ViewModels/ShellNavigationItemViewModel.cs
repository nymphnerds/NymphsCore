using NymphsCoreManager.Models;

namespace NymphsCoreManager.ViewModels;

public sealed class ShellNavigationItemViewModel
{
    public ShellNavigationItemViewModel(
        string label,
        string subtitle,
        string monogram,
        string glyph,
        string accentBrush,
        ManagerPageKind pageKind,
        NymphModuleViewModel? module = null)
    {
        Label = label;
        Subtitle = subtitle;
        Monogram = monogram;
        Glyph = glyph;
        AccentBrush = accentBrush;
        PageKind = pageKind;
        Module = module;
    }

    public string Label { get; }

    public string Subtitle { get; }

    public string Monogram { get; }

    public string Glyph { get; }

    public string AccentBrush { get; }

    public ManagerPageKind PageKind { get; }

    public NymphModuleViewModel? Module { get; }
}
