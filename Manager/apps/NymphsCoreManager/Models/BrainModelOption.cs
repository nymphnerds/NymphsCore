namespace NymphsCoreManager.Models;

public sealed class BrainModelOption
{
    public BrainModelOption(string id, string title, string description, string modelId, string quantization, int contextLength)
    {
        Id = id;
        Title = title;
        Description = description;
        ModelId = modelId;
        Quantization = quantization;
        ContextLength = contextLength;
    }

    public string Id { get; }

    public string Title { get; }

    public string Description { get; }

    public string ModelId { get; }

    public string Quantization { get; }

    public int ContextLength { get; }

    public bool IsCustom => Id == "custom";
}
