namespace ToolWebGetSimpleFunctions.Functions.Models.Source;

public sealed class SourceDocument
{
    public string Url { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public List<SourceLink> LinkDetails { get; set; } = [];
    public List<string> Links { get; set; } = [];
    public List<string> ImageAltTexts { get; set; } = [];
}
