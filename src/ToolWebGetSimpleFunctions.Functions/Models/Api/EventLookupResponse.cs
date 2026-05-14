namespace ToolWebGetSimpleFunctions.Functions.Models.Api;

public sealed class EventLookupResponse
{
    public string Facility { get; set; } = string.Empty;
    public string SearchDate { get; set; } = string.Empty;
    public string GeneratedAt { get; set; } = string.Empty;
    public EventDto? ActiveEvent { get; set; }
    public EventDto? NextEvent { get; set; }
    public List<string> Warnings { get; set; } = [];
    public SourceSummaryDto SourceSummary { get; set; } = new();
}
