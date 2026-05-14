using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using NymphsCoreManager.ViewModels;

namespace NymphsCoreManager.Models;

public sealed class NymphModuleActionGroupInfo
{
    public NymphModuleActionGroupInfo(
        string id,
        string title,
        string description,
        string entryPoint,
        string resultMode,
        string visibility,
        string submitLabel,
        IReadOnlyList<NymphModuleActionLinkInfo> links,
        IReadOnlyList<NymphModuleActionFieldInfo> fields)
    {
        Id = id;
        Title = title;
        Description = description;
        EntryPoint = entryPoint;
        ResultMode = resultMode;
        Visibility = visibility;
        SubmitLabel = submitLabel;
        Links = links;
        Fields = fields;
    }

    public string Id { get; }

    public string Title { get; }

    public string Description { get; }

    public string EntryPoint { get; }

    public string ResultMode { get; }

    public string Visibility { get; }

    public string SubmitLabel { get; }

    public IReadOnlyList<NymphModuleActionLinkInfo> Links { get; }

    public IReadOnlyList<NymphModuleActionFieldInfo> Fields { get; }

    public IReadOnlyList<NymphModuleActionFieldInfo> SecretFields =>
        Fields.Where(field => field.IsSecret).ToArray();

    public IReadOnlyList<NymphModuleActionFieldInfo> OptionFields =>
        Fields.Where(field => field.IsOptionField).ToArray();

    public bool HasLinks => Links.Count > 0;

    public bool HasFields => Fields.Count > 0;

    public bool HasSecretFields => Fields.Any(field => field.IsSecret);

    public bool HasOptionFields => Fields.Any(field => field.IsOptionField);

    public bool HasNoOptionFields => !HasOptionFields;

    public int FieldRowLeftMargin => HasOptionFields ? 0 : 24;

    public Thickness FieldRowMargin => new(FieldRowLeftMargin, 0, 0, 6);

    public double FieldLabelWidth => HasOptionFields ? 145 : double.NaN;

    public string FieldLabelAlignment => HasOptionFields ? "Right" : "Left";

    public int FieldControlWidth => 220;

    public int SavedSecretMaskWidth => FieldControlWidth - 16;

    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);

    public void ApplyFieldStateFrom(NymphModuleActionGroupInfo previous)
    {
        foreach (var field in Fields)
        {
            var previousField = previous.Fields.FirstOrDefault(candidate =>
                string.Equals(candidate.Name, field.Name, StringComparison.OrdinalIgnoreCase));
            if (previousField is not null)
            {
                field.ApplyTransientStateFrom(previousField);
            }
        }
    }
}

public sealed record NymphModuleActionLinkInfo(string Label, string Url);

public sealed class NymphModuleActionFieldInfo : ViewModelBase
{
    private string _selectedValue;
    private NymphModuleActionOptionInfo? _selectedOption;
    private string _secretValue = string.Empty;
    private bool _hasSavedSecret;

    public NymphModuleActionFieldInfo(
        string name,
        string type,
        string label,
        string defaultValue,
        string argumentName,
        string environmentName,
        string secretId,
        bool optional,
        IReadOnlyList<NymphModuleActionOptionInfo> options)
    {
        Name = name;
        Type = string.IsNullOrWhiteSpace(type) ? "select" : type;
        Label = NormalizeLabel(label, name, secretId);
        DefaultValue = defaultValue;
        ArgumentName = argumentName;
        EnvironmentName = environmentName;
        SecretId = secretId;
        Optional = optional;
        Options = options;
        _selectedValue = !string.IsNullOrWhiteSpace(defaultValue)
            ? defaultValue
            : options.Count > 0
                ? options[0].Value
                : string.Empty;
        _selectedOption = options.FirstOrDefault(option => string.Equals(option.Value, _selectedValue, StringComparison.Ordinal)) ??
                          options.FirstOrDefault();
        if (_selectedOption is not null)
        {
            _selectedValue = _selectedOption.Value;
        }
    }

    public string Name { get; }

    public string Type { get; }

    public string Label { get; }

    public string DefaultValue { get; }

    public string ArgumentName { get; }

    public string EnvironmentName { get; }

    public string SecretId { get; }

    public bool Optional { get; }

    public IReadOnlyList<NymphModuleActionOptionInfo> Options { get; }

    public string SelectedValue
    {
        get => _selectedValue;
        set
        {
            var normalizedValue = value ?? string.Empty;
            if (!SetProperty(ref _selectedValue, normalizedValue))
            {
                return;
            }

            var selectedOption = Options.FirstOrDefault(option =>
                string.Equals(option.Value, normalizedValue, StringComparison.Ordinal));
            if (!Equals(_selectedOption, selectedOption))
            {
                _selectedOption = selectedOption;
                OnPropertyChanged(nameof(SelectedOption));
            }
        }
    }

    public NymphModuleActionOptionInfo? SelectedOption
    {
        get => _selectedOption;
        set
        {
            if (!SetProperty(ref _selectedOption, value))
            {
                return;
            }

            var selectedValue = value?.Value ?? string.Empty;
            if (!string.Equals(_selectedValue, selectedValue, StringComparison.Ordinal))
            {
                _selectedValue = selectedValue;
                OnPropertyChanged(nameof(SelectedValue));
            }
        }
    }

    public string SecretValue
    {
        get => _secretValue;
        set => SetProperty(ref _secretValue, value ?? string.Empty);
    }

    public bool HasSavedSecret
    {
        get => _hasSavedSecret;
        private set
        {
            if (SetProperty(ref _hasSavedSecret, value))
            {
                OnPropertyChanged(nameof(SecretStatusLabel));
                OnPropertyChanged(nameof(SavedSecretMask));
                OnPropertyChanged(nameof(ShowSecretInput));
                OnPropertyChanged(nameof(ShowSavedSecretMask));
            }
        }
    }

    public bool IsSecret => string.Equals(Type, "secret", StringComparison.OrdinalIgnoreCase);

    public bool IsOptionField => !IsSecret && Options.Count > 0;

    public bool ShowSecretInput => IsSecret && !HasSavedSecret;

    public bool ShowSavedSecretMask => IsSecret && HasSavedSecret;

    public string SecretStatusLabel => HasSavedSecret ? "secret saved" : "no secret";

    public string SavedSecretMask => HasSavedSecret ? "••••••••••••••••••••••••••••••••••••••••••••••••" : string.Empty;

    public string SecretInputToolTip => $"Optional {Label}. The Manager saves it for future module actions.";

    public string ClearSecretToolTip => $"Remove the saved {Label} from this PC";

    public void ApplySavedSecretState(bool hasSavedSecret)
    {
        HasSavedSecret = hasSavedSecret;
    }

    public void ApplyTransientStateFrom(NymphModuleActionFieldInfo previous)
    {
        if (IsOptionField &&
            Options.Any(option => string.Equals(option.Value, previous.SelectedValue, StringComparison.Ordinal)))
        {
            SelectedValue = previous.SelectedValue;
        }

        if (IsSecret && !string.IsNullOrWhiteSpace(previous.SecretValue))
        {
            SecretValue = previous.SecretValue;
        }
    }

    private static string NormalizeLabel(string label, string name, string secretId)
    {
        if (string.Equals(secretId, "huggingface.token", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(label, "HF", StringComparison.OrdinalIgnoreCase))
        {
            return "Hugging Face token";
        }

        if (string.Equals(secretId, "openrouter.api_key", StringComparison.OrdinalIgnoreCase))
        {
            return "OpenRouter key";
        }

        return string.IsNullOrWhiteSpace(label) ? name : label;
    }
}

public sealed record NymphModuleActionOptionInfo(
    string Label,
    string Value,
    string Description)
{
    public override string ToString()
    {
        return Label;
    }
}
