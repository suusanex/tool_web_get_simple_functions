using ToolWebGetSimpleFunctions.Functions.Models.Api;
using ToolWebGetSimpleFunctions.Functions.Models.Extraction;

namespace ToolWebGetSimpleFunctions.Functions.Services;

public interface IEventSelectionService
{
    (EventDto? ActiveEvent, EventDto? NextEvent, List<string> Warnings) Select(DateOnly searchDate, IEnumerable<ExtractedEventCandidate> candidates);
}
