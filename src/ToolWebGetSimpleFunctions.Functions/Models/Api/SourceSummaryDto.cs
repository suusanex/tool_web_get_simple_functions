namespace ToolWebGetSimpleFunctions.Functions.Models.Api;

public sealed class SourceSummaryDto
{
    public string OfficialNewsUrl { get; set; } = string.Empty;
    public List<string> FetchedUrls { get; set; } = [];
    public int CandidateArticleCount { get; set; }
    public int ExtractionCandidateCount { get; set; }
    public int ValidatedCandidateCount { get; set; }
}
