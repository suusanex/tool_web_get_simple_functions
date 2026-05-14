using ToolWebGetSimpleFunctions.Functions.Models.Extraction;
using ToolWebGetSimpleFunctions.Functions.Models.Source;
using ToolWebGetSimpleFunctions.Functions.Services;
using Xunit;

namespace ToolWebGetSimpleFunctions.Functions.Tests.Services;

public sealed class EventCandidateValidatorTests
{
    [Fact]
    public void Validate_WhenValuesAreTraceable_ReturnsValidCandidate()
    {
        var validator = new EventCandidateValidator();
        var input = BuildInput();
        var candidate = new ExtractedEventCandidate
        {
            Title = "コラボ開催決定 RAKU SPA 1010 神田",
            StartDate = "2026-05-28",
            EndDate = "2026-06-30",
            VenueConfirmed = true,
            VenueEvidence = "RAKU SPA 1010 神田で開催",
            EventType = "collaboration",
            SourceUrls = ["https://rakuspa.com/kanda/news/123"],
            EvidenceSnippets = ["コラボ開催決定", "RAKU SPA 1010 神田で開催"],
            Confidence = "high"
        };

        var (validCandidates, warnings) = validator.Validate(input, [candidate]);

        Assert.Single(validCandidates);
        Assert.Empty(warnings);
    }

    [Fact]
    public void Validate_WhenVenueEvidenceIsNotTraceable_RejectsCandidate()
    {
        var validator = new EventCandidateValidator();
        var input = BuildInput();
        var candidate = new ExtractedEventCandidate
        {
            Title = "コラボ開催決定 RAKU SPA 1010 神田",
            StartDate = "2026-05-28",
            EndDate = "2026-06-30",
            VenueConfirmed = true,
            VenueEvidence = "都内某所で開催",
            EventType = "collaboration",
            SourceUrls = ["https://rakuspa.com/kanda/news/123"],
            EvidenceSnippets = ["コラボ開催決定"],
            Confidence = "high"
        };

        var (validCandidates, warnings) = validator.Validate(input, [candidate]);

        Assert.Empty(validCandidates);
        Assert.Contains(warnings, w => w.Contains("venue evidence is not traceable", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_WhenDateIsNotTraceable_RejectsCandidate()
    {
        var validator = new EventCandidateValidator();
        var input = BuildInput();
        var candidate = new ExtractedEventCandidate
        {
            Title = "コラボ開催決定 RAKU SPA 1010 神田",
            StartDate = "2027-01-01",
            EndDate = "2027-01-31",
            VenueConfirmed = true,
            VenueEvidence = "RAKU SPA 1010 神田で開催",
            EventType = "collaboration",
            SourceUrls = ["https://rakuspa.com/kanda/news/123"],
            EvidenceSnippets = ["コラボ開催決定"],
            Confidence = "high"
        };

        var (validCandidates, warnings) = validator.Validate(input, [candidate]);

        Assert.Empty(validCandidates);
        Assert.Contains(warnings, w => w.Contains("date values are not traceable", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_WhenEvidenceSnippetIsNotTraceable_RejectsCandidate()
    {
        var validator = new EventCandidateValidator();
        var input = BuildInput();
        var candidate = new ExtractedEventCandidate
        {
            Title = "コラボ開催決定 RAKU SPA 1010 神田",
            StartDate = "2026-05-28",
            EndDate = "2026-06-30",
            VenueConfirmed = true,
            VenueEvidence = "RAKU SPA 1010 神田で開催",
            EventType = "collaboration",
            SourceUrls = ["https://rakuspa.com/kanda/news/123"],
            EvidenceSnippets = ["入力に存在しない文言"],
            Confidence = "high"
        };

        var (validCandidates, warnings) = validator.Validate(input, [candidate]);

        Assert.Empty(validCandidates);
        Assert.Contains(warnings, w => w.Contains("evidence snippets are not traceable", StringComparison.OrdinalIgnoreCase));
    }

    private static ExtractionInput BuildInput()
    {
        return new ExtractionInput
        {
            SearchDate = "2026-05-29",
            FacilityName = "RAKU SPA 1010 神田",
            FacilityAliases = ["RAKU SPA 1010 神田", "らくスパ 1010 神田"],
            DetectedDateLikeStrings = ["2026/5/28", "2026-06-30"],
            DetectedVenueLikeStrings = ["RAKU SPA 1010 神田"],
            Documents =
            [
                new SourceDocument
                {
                    Url = "https://rakuspa.com/kanda/news/123",
                    SourceType = "news_article",
                    Title = "コラボ開催決定 RAKU SPA 1010 神田",
                    Text = "2026/5/28から2026-06-30までRAKU SPA 1010 神田で開催",
                    ImageAltTexts = ["RAKU SPA 1010 神田"]
                }
            ]
        };
    }
}
