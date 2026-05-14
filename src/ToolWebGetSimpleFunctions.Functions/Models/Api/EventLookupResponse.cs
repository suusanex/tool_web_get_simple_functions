using System.Text.Json.Serialization;

namespace ToolWebGetSimpleFunctions.Functions.Models.Api;

public sealed class EventLookupResponse
{
    [JsonPropertyName("facility")]
    public string Facility { get; set; } = string.Empty;

    [JsonPropertyName("searchDate")]
    public string SearchDate { get; set; } = string.Empty;

    [JsonPropertyName("generatedAt")]
    public string GeneratedAt { get; set; } = string.Empty;

    [JsonPropertyName("activeEvent")]
    public EventDto? ActiveEvent { get; set; }

    [JsonPropertyName("nextEvent")]
    public EventDto? NextEvent { get; set; }

    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; set; } = [];

    [JsonPropertyName("sourceSummary")]
    public SourceSummaryDto SourceSummary { get; set; } = new();
}
