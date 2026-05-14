using ToolWebGetSimpleFunctions.Functions.Models.Api;
using Microsoft.Extensions.Logging;
using ToolWebGetSimpleFunctions.Functions.Options;
using Microsoft.Extensions.Options;

namespace ToolWebGetSimpleFunctions.Functions.Services;

public sealed class RakuSpaEventLookupService : IRakuSpaEventLookupService
{
    private readonly ISourceCollector _sourceCollector;
    private readonly IAzureOpenAIExtractionClient _extractionClient;
    private readonly IEventCandidateValidator _validator;
    private readonly IEventSelectionService _selector;
    private readonly IClock _clock;
    private readonly ApplicationOptions _applicationOptions;
    private readonly ILogger<RakuSpaEventLookupService> _logger;

    public RakuSpaEventLookupService(
        ISourceCollector sourceCollector,
        IAzureOpenAIExtractionClient extractionClient,
        IEventCandidateValidator validator,
        IEventSelectionService selector,
        IClock clock,
        IOptions<ApplicationOptions> applicationOptions,
        ILogger<RakuSpaEventLookupService> logger)
    {
        _sourceCollector = sourceCollector;
        _extractionClient = extractionClient;
        _validator = validator;
        _selector = selector;
        _clock = clock;
        _applicationOptions = applicationOptions.Value;
        _logger = logger;
    }

    public async Task<EventLookupResponse> LookupAsync(DateOnly searchDate, CancellationToken cancellationToken)
    {
        try
        {
            var correlationId = Guid.NewGuid().ToString("N");
            _logger.LogInformation("Event lookup started. CorrelationId={CorrelationId} SearchDate={SearchDate}", correlationId, searchDate.ToString("yyyy-MM-dd"));

            var (input, summary) = await _sourceCollector.CollectAsync(searchDate, cancellationToken).ConfigureAwait(false);
            var extraction = await _extractionClient.ExtractAsync(input, cancellationToken).ConfigureAwait(false);
            var (validCandidates, validationWarnings) = _validator.Validate(input, extraction.Candidates);
            var (activeEvent, nextEvent, selectionWarnings) = _selector.Select(searchDate, validCandidates);

            summary.ExtractionCandidateCount = extraction.Candidates.Count;
            summary.ValidatedCandidateCount = validCandidates.Count;

            var warnings = new List<string>();
            warnings.AddRange(extraction.Warnings);
            warnings.AddRange(validationWarnings);
            warnings.AddRange(selectionWarnings);
            if (activeEvent is null && nextEvent is null)
            {
                warnings.Add("No matching collaboration event was found.");
            }

            var response = new EventLookupResponse
            {
                Facility = input.FacilityName,
                SearchDate = searchDate.ToString("yyyy-MM-dd"),
                GeneratedAt = _clock.UtcNow.ToOffset(GetOffset()).ToString("yyyy-MM-ddTHH:mm:sszzz"),
                ActiveEvent = activeEvent,
                NextEvent = nextEvent,
                Warnings = warnings,
                SourceSummary = summary
            };

            _logger.LogInformation("Event lookup completed. CorrelationId={CorrelationId} ActiveEvent={HasActive} NextEvent={HasNext} WarningCount={WarningCount}", correlationId, response.ActiveEvent is not null, response.NextEvent is not null, response.Warnings.Count);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Event lookup failed: {ExceptionText}", ex.ToString());
            throw;
        }
    }

    private TimeSpan GetOffset()
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById(_applicationOptions.TimeZone);
        return tz.GetUtcOffset(_clock.UtcNow);
    }
}
