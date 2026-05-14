using ToolWebGetSimpleFunctions.Functions.Models.Extraction;
using ToolWebGetSimpleFunctions.Functions.Models.Source;

namespace ToolWebGetSimpleFunctions.Functions.Services;

public interface IEventCandidateValidator
{
    (List<ExtractedEventCandidate> ValidCandidates, List<string> Warnings) Validate(ExtractionInput input, IEnumerable<ExtractedEventCandidate> candidates);
}
