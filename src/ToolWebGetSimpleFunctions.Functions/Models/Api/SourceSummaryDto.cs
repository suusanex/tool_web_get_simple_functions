using System.Text.Json.Serialization;

namespace ToolWebGetSimpleFunctions.Functions.Models.Api;

public sealed class SourceSummaryDto
{
    [JsonPropertyName("officialNewsUrl")]
    public string OfficialNewsUrl { get; set; } = string.Empty;

    [JsonPropertyName("fetchedUrls")]
    public List<string> FetchedUrls { get; set; } = [];

    [JsonPropertyName("candidateArticleCount")]
    public int CandidateArticleCount { get; set; }

    [JsonPropertyName("extractionCandidateCount")]
    public int ExtractionCandidateCount { get; set; }

    [JsonPropertyName("validatedCandidateCount")]
    public int ValidatedCandidateCount { get; set; }
}
