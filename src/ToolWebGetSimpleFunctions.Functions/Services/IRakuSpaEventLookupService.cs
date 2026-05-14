using ToolWebGetSimpleFunctions.Functions.Models.Api;

namespace ToolWebGetSimpleFunctions.Functions.Services;

public interface IRakuSpaEventLookupService
{
    Task<EventLookupResponse> LookupAsync(DateOnly searchDate, CancellationToken cancellationToken);
}
