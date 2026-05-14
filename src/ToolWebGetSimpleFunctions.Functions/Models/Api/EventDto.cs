namespace ToolWebGetSimpleFunctions.Functions.Models.Api;

public sealed class EventDto
{
    public string Title { get; set; } = string.Empty;
    public string StartDate { get; set; } = string.Empty;
    public string EndDate { get; set; } = string.Empty;
    public List<string> InitialPeakDates { get; set; } = [];
    public string BusyPhase { get; set; } = "unknown";
    public string Confidence { get; set; } = "low";
    public List<string> SourceUrls { get; set; } = [];
}
