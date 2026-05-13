using System;
using System.Collections.Generic;
using System.Linq;
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

    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);
}

public sealed record NymphModuleActionLinkInfo(string Label, string Url);

public sealed class NymphModuleActionFieldInfo : ViewModelBase
{
    private string _selectedValue;
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
        set => SetProperty(ref _selectedValue, value ?? string.Empty);
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

    public string SecretStatusLabel => HasSavedSecret ? "token saved" : "no token";

    public string SavedSecretMask => HasSavedSecret ? "••••••••••••••••••••••••••••••••" : string.Empty;

    public void ApplySavedSecretState(bool hasSavedSecret)
    {
        HasSavedSecret = hasSavedSecret;
    }

    private static string NormalizeLabel(string label, string name, string secretId)
    {
        if (string.Equals(secretId, "huggingface.token", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(label, "HF", StringComparison.OrdinalIgnoreCase))
        {
            return "Hugging Face token";
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
