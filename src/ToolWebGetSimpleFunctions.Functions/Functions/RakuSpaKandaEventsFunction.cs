using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using ToolWebGetSimpleFunctions.Functions.Options;
using ToolWebGetSimpleFunctions.Functions.Services;
using Microsoft.Extensions.Options;

namespace ToolWebGetSimpleFunctions.Functions.Functions;

public sealed class RakuSpaKandaEventsFunction
{
    private readonly IRakuSpaEventLookupService _service;
    private readonly IClock _clock;
    private readonly ApplicationOptions _applicationOptions;
    private readonly ILogger<RakuSpaKandaEventsFunction> _logger;

    public RakuSpaKandaEventsFunction(
        IRakuSpaEventLookupService service,
        IClock clock,
        IOptions<ApplicationOptions> applicationOptions,
        ILogger<RakuSpaKandaEventsFunction> logger)
    {
        _service = service;
        _clock = clock;
        _applicationOptions = applicationOptions.Value;
        _logger = logger;
    }

    [Function("RakuSpaKandaEvents")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "rakuspa/kanda/events")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (_applicationOptions.RequireAuthenticatedUser && !IsAuthenticated(request))
            {
                _logger.LogWarning("Unauthorized request was rejected.");
                var unauthorized = request.CreateResponse(System.Net.HttpStatusCode.Unauthorized);
                await unauthorized.WriteStringAsync("Authentication is required.", cancellationToken).ConfigureAwait(false);
                return unauthorized;
            }

            var query = System.Web.HttpUtility.ParseQueryString(request.Url.Query);
            var searchDate = ResolveSearchDate(query["date"]);
            var result = await _service.LookupAsync(searchDate, cancellationToken).ConfigureAwait(false);

            var ok = request.CreateResponse(System.Net.HttpStatusCode.OK);
            await ok.WriteAsJsonAsync(result, cancellationToken).ConfigureAwait(false);
            return ok;
        }
        catch (FormatException ex)
        {
            _logger.LogError(ex, "Bad request: {ExceptionText}", ex.ToString());
            var bad = request.CreateResponse(System.Net.HttpStatusCode.BadRequest);
            await bad.WriteStringAsync("Invalid date format. Use YYYY-MM-DD.", cancellationToken).ConfigureAwait(false);
            return bad;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error: {ExceptionText}", ex.ToString());
            var error = request.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            await error.WriteStringAsync("Internal Server Error.", cancellationToken).ConfigureAwait(false);
            return error;
        }
    }

    private DateOnly ResolveSearchDate(string? dateText)
    {
        if (!string.IsNullOrWhiteSpace(dateText))
        {
            if (DateOnly.TryParseExact(dateText, "yyyy-MM-dd", out var parsed))
            {
                return parsed;
            }

            throw new FormatException($"Invalid date format: {dateText}");
        }

        var tz = TimeZoneInfo.FindSystemTimeZoneById(_applicationOptions.TimeZone);
        var localNow = TimeZoneInfo.ConvertTime(_clock.UtcNow, tz);
        return DateOnly.FromDateTime(localNow.DateTime);
    }

    private static bool IsAuthenticated(HttpRequestData request)
        => request.Headers.TryGetValues("x-ms-client-principal-id", out var principalIds) && principalIds.Any(v => !string.IsNullOrWhiteSpace(v))
            || request.Headers.TryGetValues("x-ms-client-principal", out var principals) && principals.Any(v => !string.IsNullOrWhiteSpace(v));
}
