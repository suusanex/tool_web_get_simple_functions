using ToolWebGetSimpleFunctions.Functions.Models.Api;
using ToolWebGetSimpleFunctions.Functions.Models.Extraction;

namespace ToolWebGetSimpleFunctions.Functions.Services;

public sealed class EventSelectionService : IEventSelectionService
{
    public (EventDto? ActiveEvent, EventDto? NextEvent, List<string> Warnings) Select(DateOnly searchDate, IEnumerable<ExtractedEventCandidate> candidates)
    {
        var warnings = new List<string>();
        var dated = candidates
            .Select(c => (Candidate: c, Start: DateOnly.Parse(c.StartDate), End: DateOnly.Parse(c.EndDate)))
            .OrderBy(x => x.Start)
            .ThenByDescending(x => x.End)
            .ThenBy(x => x.Candidate.Title, StringComparer.Ordinal)
            .ToList();

        var activeList = dated.Where(x => x.Start <= searchDate && searchDate <= x.End).ToList();
        var nextList = dated.Where(x => x.Start > searchDate).ToList();

        if (activeList.Count > 1)
        {
            warnings.Add("Multiple active candidates found. Deterministic ordering was applied.");
        }

        if (nextList.Count > 1 && nextList[0].Start == nextList[1].Start)
        {
            warnings.Add("Multiple next candidates with the same start date were found.");
        }

        return (
            activeList.Count == 0 ? null : ToEventDto(activeList[0].Candidate, searchDate),
            nextList.Count == 0 ? null : ToEventDto(nextList[0].Candidate, searchDate),
            warnings);
    }

    private static EventDto ToEventDto(ExtractedEventCandidate candidate, DateOnly searchDate)
    {
        var start = DateOnly.Parse(candidate.StartDate);
        var end = DateOnly.Parse(candidate.EndDate);
        var initialPeak = new List<string>
        {
            start.ToString("yyyy-MM-dd"),
            start.AddDays(1).ToString("yyyy-MM-dd"),
            start.AddDays(2).ToString("yyyy-MM-dd")
        };

        return new EventDto
        {
            Title = candidate.Title,
            StartDate = candidate.StartDate,
            EndDate = candidate.EndDate,
            InitialPeakDates = initialPeak,
            BusyPhase = ClassifyBusyPhase(searchDate, start, end),
            Confidence = candidate.Confidence,
            SourceUrls = candidate.SourceUrls
        };
    }

    private static string ClassifyBusyPhase(DateOnly searchDate, DateOnly startDate, DateOnly endDate)
    {
        if (searchDate < startDate)
        {
            return "before_event";
        }

        if (searchDate >= startDate && searchDate <= startDate.AddDays(2))
        {
            return "initial_peak";
        }

        if (searchDate > startDate.AddDays(2) && searchDate <= endDate)
        {
            return "after_initial_peak";
        }

        return "unknown";
    }
}
