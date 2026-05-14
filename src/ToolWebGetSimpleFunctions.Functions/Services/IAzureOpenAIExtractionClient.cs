using ToolWebGetSimpleFunctions.Functions.Models.Extraction;
using ToolWebGetSimpleFunctions.Functions.Models.Source;

namespace ToolWebGetSimpleFunctions.Functions.Services;

public interface IAzureOpenAIExtractionClient
{
    Task<ExtractionResult> ExtractAsync(ExtractionInput input, CancellationToken cancellationToken);
}
