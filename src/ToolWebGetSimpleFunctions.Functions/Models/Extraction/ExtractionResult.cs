namespace ToolWebGetSimpleFunctions.Functions.Models.Extraction;

public sealed class ExtractionResult
{
    public List<ExtractedEventCandidate> Candidates { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
}
