using ToolWebGetSimpleFunctions.Functions.Models.Api;
using ToolWebGetSimpleFunctions.Functions.Models.Source;

namespace ToolWebGetSimpleFunctions.Functions.Services;

public interface ISourceCollector
{
    Task<(ExtractionInput Input, SourceSummaryDto Summary)> CollectAsync(DateOnly searchDate, CancellationToken cancellationToken);
}
