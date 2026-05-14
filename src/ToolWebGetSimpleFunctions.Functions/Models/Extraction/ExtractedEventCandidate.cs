namespace ToolWebGetSimpleFunctions.Functions.Models.Extraction;

public sealed class ExtractedEventCandidate
{
    public string Title { get; set; } = string.Empty;
    public string StartDate { get; set; } = string.Empty;
    public string EndDate { get; set; } = string.Empty;
    public bool VenueConfirmed { get; set; }
    public string EventType { get; set; } = string.Empty;
    public List<string> SourceUrls { get; set; } = [];
    public List<string> EvidenceSnippets { get; set; } = [];
    public string Confidence { get; set; } = "low";
    public List<string> Warnings { get; set; } = [];
}
