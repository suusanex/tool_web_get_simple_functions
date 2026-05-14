namespace ToolWebGetSimpleFunctions.Functions.Models.Source;

public sealed class CandidateArticle
{
    public required SourceDocument Article { get; init; }
    public List<SourceDocument> LinkedCampaignDocuments { get; init; } = [];
}
