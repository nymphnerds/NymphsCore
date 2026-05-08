using NymphsCoreManager.Models;

namespace NymphsCoreManager.ViewModels;

/// <summary>
/// Lightweight card ViewModel for the home page module grid.
/// Displays a subset of NymphModuleViewModel properties optimized for card rendering.
/// </summary>
public sealed class NymphModuleCardViewModel : ViewModelBase
{
    private readonly NymphModuleViewModel _module;

    public NymphModuleCardViewModel(NymphModuleViewModel module)
    {
        _module = module;
    }

    /// <summary>
    /// The underlying NymphModuleViewModel.
    /// </summary>
    public NymphModuleViewModel Module => _module;

    /// <summary>
    /// Module unique identifier.
    /// </summary>
    public string Id => _module.Id;

    /// <summary>
    /// Display name (from ui.tab_label or definition name).
    /// </summary>
    public string Name => _module.Name;

    /// <summary>
    /// Two-letter monogram for the card avatar.
    /// </summary>
    public string Monogram => _module.Monogram;

    /// <summary>
    /// Module description for the card subtitle.
    /// </summary>
    public string Description => _module.Description;

    /// <summary>
    /// Accent brush color for the card border/highlight.
    /// </summary>
    public string AccentBrush => _module.AccentBrush;

    /// <summary>
    /// Module kind (repo, archive, hybrid, script) — used in installed card template.
    /// </summary>
    public string Kind => _module.Kind;

    /// <summary>
    /// Module category (runtime, tool, service, trainer, etc.) — used in available card template.
    /// </summary>
    public string Category => _module.Category;

    /// <summary>
    /// Status brush for available (not-installed) module cards.
    /// </summary>
    public string StatusBrush => _module.StatusBrush;

    /// <summary>
    /// Whether the module is currently installed.
    /// </summary>
    public bool IsInstalled => _module.IsInstalled;

    /// <summary>
    /// Whether the module is currently running.
    /// </summary>
    public bool IsRunning => _module.IsRunning;

    /// <summary>
    /// Display state label (e.g. "Running", "Stopped", "Update available").
    /// </summary>
    public string DisplayStateLabel => _module.DisplayStateLabel;

    /// <summary>
    /// Brush color for the state badge.
    /// </summary>
    public string DisplayStatusBrush => _module.DisplayStatusBrush;

    /// <summary>
    /// Whether an update is available for this module.
    /// </summary>
    public bool HasUpdate => _module.HasUpdate;

    /// <summary>
    /// Label for the install button on available module cards.
    /// Falls back to "Install {Name}" if not specified in manifest.
    /// </summary>
    public string InstallLabel => $"Install {Name}";

    /// <summary>
    /// Label shown in "Your Nymphs" vs "Available Nymphs" sections.
    /// </summary>
    public string AvailabilityLabel => _module.AvailabilityLabel;

    /// <summary>
    /// Subtitle for navigation/sidebar display.
    /// </summary>
    public string NavigationSubtitle => _module.NavigationSubtitle;

    /// <summary>
    /// Version string detected on disk.
    /// </summary>
    public string VersionLabel => _module.VersionLabel;

    /// <summary>
    /// Remote version from the registry.
    /// </summary>
    public string RemoteVersionLabel => _module.RemoteVersionLabel;
}