using ToolWebGetSimpleFunctions.Functions.Models.Extraction;
using ToolWebGetSimpleFunctions.Functions.Services;
using Xunit;

namespace ToolWebGetSimpleFunctions.Functions.Tests.Services;

public sealed class EventSelectionServiceTests
{
    [Fact]
    public void Select_WhenDateIsStartPlusTwo_ClassifiesAsInitialPeak()
    {
        var service = new EventSelectionService();

        var (activeEvent, nextEvent, warnings) = service.Select(
            new DateOnly(2026, 5, 3),
            [CreateCandidate("A", "2026-05-01", "2026-05-10")]);

        Assert.NotNull(activeEvent);
        Assert.Equal("initial_peak", activeEvent!.BusyPhase);
        Assert.Equal(["2026-05-01", "2026-05-02", "2026-05-03"], activeEvent.InitialPeakDates);
        Assert.Null(nextEvent);
        Assert.Empty(warnings);
    }

    [Fact]
    public void Select_WhenShortEvent_CapsInitialPeakDatesAtEndDate()
    {
        var service = new EventSelectionService();

        var (activeEvent, _, _) = service.Select(
            new DateOnly(2026, 5, 2),
            [CreateCandidate("A", "2026-05-01", "2026-05-02")]);

        Assert.NotNull(activeEvent);
        Assert.Equal(["2026-05-01", "2026-05-02"], activeEvent!.InitialPeakDates);
    }

    [Fact]
    public void Select_WhenMultipleActiveEvents_SelectsDeterministicallyAndWarns()
    {
        var service = new EventSelectionService();

        var (activeEvent, _, warnings) = service.Select(
            new DateOnly(2026, 5, 5),
            [
                CreateCandidate("B", "2026-05-03", "2026-05-10"),
                CreateCandidate("A", "2026-05-01", "2026-05-06")
            ]);

        Assert.NotNull(activeEvent);
        Assert.Equal("A", activeEvent!.Title);
        Assert.Contains(warnings, warning => warning.Contains("Multiple active candidates", StringComparison.Ordinal));
    }

    [Fact]
    public void Select_WhenMultipleNextEventsShareStartDate_SelectsDeterministicallyAndWarns()
    {
        var service = new EventSelectionService();

        var (activeEvent, nextEvent, warnings) = service.Select(
            new DateOnly(2026, 5, 1),
            [
                CreateCandidate("B", "2026-05-10", "2026-05-12"),
                CreateCandidate("A", "2026-05-10", "2026-05-15")
            ]);

        Assert.Null(activeEvent);
        Assert.NotNull(nextEvent);
        Assert.Equal("A", nextEvent!.Title);
        Assert.Contains(warnings, warning => warning.Contains("same start date", StringComparison.Ordinal));
    }

    [Fact]
    public void Select_WhenNoCandidates_ReturnsNoEvent()
    {
        var service = new EventSelectionService();

        var (activeEvent, nextEvent, warnings) = service.Select(new DateOnly(2026, 5, 1), []);

        Assert.Null(activeEvent);
        Assert.Null(nextEvent);
        Assert.Empty(warnings);
    }

    private static ExtractedEventCandidate CreateCandidate(string title, string startDate, string endDate)
        => new()
        {
            Title = title,
            StartDate = startDate,
            EndDate = endDate,
            VenueConfirmed = true,
            VenueEvidence = "RAKU SPA 1010 神田",
            EventType = "collaboration",
            SourceUrls = ["https://example.invalid/article"],
            EvidenceSnippets = ["snippet"],
            Confidence = "high"
        };
}
