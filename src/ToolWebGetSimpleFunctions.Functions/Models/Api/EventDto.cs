using System.Text.Json.Serialization;

namespace ToolWebGetSimpleFunctions.Functions.Models.Api;

public sealed class EventDto
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("startDate")]
    public string StartDate { get; set; } = string.Empty;

    [JsonPropertyName("endDate")]
    public string EndDate { get; set; } = string.Empty;

    [JsonPropertyName("initialPeakDates")]
    public List<string> InitialPeakDates { get; set; } = [];

    [JsonPropertyName("busyPhase")]
    public string BusyPhase { get; set; } = "unknown";

    [JsonPropertyName("confidence")]
    public string Confidence { get; set; } = "low";

    [JsonPropertyName("sourceUrls")]
    public List<string> SourceUrls { get; set; } = [];
}
