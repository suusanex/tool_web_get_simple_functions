namespace ToolWebGetSimpleFunctions.Functions.Models.Source;

public sealed class ExtractionInput
{
    public string SearchDate { get; set; } = string.Empty;
    public string FacilityName { get; set; } = string.Empty;
    public List<string> FacilityAliases { get; set; } = [];
    public List<SourceDocument> Documents { get; set; } = [];
    public List<string> DetectedDateLikeStrings { get; set; } = [];
    public List<string> DetectedVenueLikeStrings { get; set; } = [];
}
