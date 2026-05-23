using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using NymphsCoreManager.ViewModels;

namespace NymphsCoreManager.Models;

public sealed class NymphModuleActionGroupInfo : ViewModelBase
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
        IReadOnlyList<NymphModuleActionFieldInfo> fields,
        IReadOnlyList<string>? showWhenStates = null)
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
        ShowWhenStates = showWhenStates ?? Array.Empty<string>();

        if (HasInlineOptionSubmit)
        {
            OptionFields[^1].SetShowsInlineGroupSubmit(true);
        }

        foreach (var field in Fields)
        {
            field.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName is nameof(NymphModuleActionFieldInfo.SelectedValue) or nameof(NymphModuleActionFieldInfo.IsChecked))
                {
                    OnPropertyChanged(nameof(CanSubmit));
                }
            };
        }
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

    public IReadOnlyList<string> ShowWhenStates { get; }

    public IReadOnlyList<NymphModuleActionFieldInfo> SecretFields =>
        Fields.Where(field => field.IsSecret).ToArray();

    public IReadOnlyList<NymphModuleActionFieldInfo> OptionFields =>
        Fields.Where(field => field.IsOptionField && !field.IsAgreementField).ToArray();

    public IReadOnlyList<NymphModuleActionFieldInfo> CheckboxFields =>
        Fields.Where(field => field.IsCheckbox && !field.IsAgreementField).ToArray();

    public bool HasLinks => Links.Count > 0;

    public bool HasFields => Fields.Count > 0;

    public bool HasNoFields => Fields.Count == 0;

    public bool HasSecretFields => Fields.Any(field => field.IsSecret);

    public bool HasOptionFields => OptionFields.Count > 0;

    public bool HasCheckboxFields => CheckboxFields.Count > 0;

    public bool HasChoiceFields => HasOptionFields || HasCheckboxFields;

    public bool HasNoChoiceFields => !HasChoiceFields;

    public bool HasInlineOptionSubmit => HasOptionFields && !HasCheckboxFields;

    public bool HasSeparateChoiceSubmit => HasChoiceFields && !HasInlineOptionSubmit;

    public bool CanSubmit => !Fields.Any(field => field.BlocksSubmit);

    public int FieldRowLeftMargin => HasChoiceFields ? 0 : 24;

    public Thickness FieldRowMargin => new(FieldRowLeftMargin, 0, 0, 6);

    public double FieldLabelWidth => HasChoiceFields ? 145 : double.NaN;

    public string FieldLabelAlignment => HasChoiceFields ? "Right" : "Left";

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
    private bool _showsInlineGroupSubmit;

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

            if (IsCheckbox)
            {
                OnPropertyChanged(nameof(IsChecked));
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
                if (IsCheckbox)
                {
                    OnPropertyChanged(nameof(IsChecked));
                }
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

    public bool IsCheckbox => string.Equals(Type, "checkbox", StringComparison.OrdinalIgnoreCase);

    public bool IsRequiredCheckbox => IsCheckbox && !Optional;

    public bool IsAgreementField =>
        string.Equals(Name, "license_ack", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(ArgumentName, "--license-ack", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(ArgumentName, "--license_ack", StringComparison.OrdinalIgnoreCase);

    public bool ShowsInlineGroupSubmit
    {
        get => _showsInlineGroupSubmit;
        private set => SetProperty(ref _showsInlineGroupSubmit, value);
    }

    public void SetShowsInlineGroupSubmit(bool value)
    {
        ShowsInlineGroupSubmit = value;
    }

    public bool BlocksSubmit => (IsRequiredCheckbox || (IsAgreementField && !Optional)) && !IsChecked;

    public bool IsOptionField => !IsSecret && !IsCheckbox && Options.Count > 0;

    public bool IsChecked
    {
        get => IsTruthy(SelectedValue);
        set => SelectedValue = value ? CheckedValue : UncheckedValue;
    }

    public string CheckedValue =>
        Options.FirstOrDefault(option => IsTruthy(option.Value))?.Value ?? "yes";

    public string UncheckedValue =>
        Options.FirstOrDefault(option => !IsTruthy(option.Value))?.Value ?? "no";

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
        if ((IsOptionField || IsCheckbox) &&
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

        if (string.Equals(name, "license_ack", StringComparison.OrdinalIgnoreCase))
        {
            return "License/access";
        }

        return string.IsNullOrWhiteSpace(label) ? name : label;
    }

    private static bool IsTruthy(string? value)
    {
        return value?.Trim().ToLowerInvariant() is "1" or "true" or "yes" or "y" or "on" or "checked" or "acknowledged";
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
