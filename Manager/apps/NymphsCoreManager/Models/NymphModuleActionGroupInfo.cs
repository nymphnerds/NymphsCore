using System;
using System.Collections.Generic;
using NymphsCoreManager.ViewModels;

namespace NymphsCoreManager.Models;

public sealed class NymphModuleActionGroupInfo
{
    public NymphModuleActionGroupInfo(
        string id,
        string title,
        string entryPoint,
        string resultMode,
        string visibility,
        string submitLabel,
        IReadOnlyList<NymphModuleActionLinkInfo> links,
        IReadOnlyList<NymphModuleActionFieldInfo> fields)
    {
        Id = id;
        Title = title;
        EntryPoint = entryPoint;
        ResultMode = resultMode;
        Visibility = visibility;
        SubmitLabel = submitLabel;
        Links = links;
        Fields = fields;
    }

    public string Id { get; }

    public string Title { get; }

    public string EntryPoint { get; }

    public string ResultMode { get; }

    public string Visibility { get; }

    public string SubmitLabel { get; }

    public IReadOnlyList<NymphModuleActionLinkInfo> Links { get; }

    public IReadOnlyList<NymphModuleActionFieldInfo> Fields { get; }

    public bool HasLinks => Links.Count > 0;

    public bool HasFields => Fields.Count > 0;
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
        Label = string.IsNullOrWhiteSpace(label) ? name : label;
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
            }
        }
    }

    public bool IsSecret => string.Equals(Type, "secret", StringComparison.OrdinalIgnoreCase);

    public bool IsOptionField => !IsSecret && Options.Count > 0;

    public string SecretStatusLabel => HasSavedSecret ? "saved" : "not saved";

    public void ApplySavedSecretState(bool hasSavedSecret)
    {
        HasSavedSecret = hasSavedSecret;
    }
}

public sealed record NymphModuleActionOptionInfo(
    string Label,
    string Value,
    string Description);
