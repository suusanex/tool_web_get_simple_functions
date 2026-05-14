using ToolWebGetSimpleFunctions.Functions.Models.Source;

namespace ToolWebGetSimpleFunctions.Functions.Services;

public interface IHtmlTextExtractor
{
    Task<SourceDocument> ExtractAsync(string html, string url, string sourceType, CancellationToken cancellationToken);
}
