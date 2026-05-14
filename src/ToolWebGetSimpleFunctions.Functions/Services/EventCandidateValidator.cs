using ToolWebGetSimpleFunctions.Functions.Models.Extraction;
using ToolWebGetSimpleFunctions.Functions.Models.Source;

namespace ToolWebGetSimpleFunctions.Functions.Services;

public sealed class EventCandidateValidator : IEventCandidateValidator
{
    public (List<ExtractedEventCandidate> ValidCandidates, List<string> Warnings) Validate(ExtractionInput input, IEnumerable<ExtractedEventCandidate> candidates)
    {
        var valid = new List<ExtractedEventCandidate>();
        var warnings = new List<string>();
        var sourceUrls = input.Documents.Select(d => d.Url).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var c in candidates)
        {
            if (string.IsNullOrWhiteSpace(c.Title))
            {
                warnings.Add("Rejected candidate because title is missing.");
                continue;
            }

            if (!DateOnly.TryParseExact(c.StartDate, "yyyy-MM-dd", out var start) || !DateOnly.TryParseExact(c.EndDate, "yyyy-MM-dd", out var end) || start > end)
            {
                warnings.Add($"Rejected candidate '{c.Title}' because date range is invalid.");
                continue;
            }

            if (!c.VenueConfirmed)
            {
                warnings.Add($"Rejected candidate '{c.Title}' because venue was not confirmed.");
                continue;
            }

            if (!c.SourceUrls.Any() || c.SourceUrls.Any(url => !sourceUrls.Contains(url)))
            {
                warnings.Add($"Rejected candidate '{c.Title}' because sourceUrls are not traceable.");
                continue;
            }

            if (!ContainsCollaborationSignal(c.EventType))
            {
                warnings.Add($"Rejected candidate '{c.Title}' because eventType is not collaboration.");
                continue;
            }

            if (!c.EvidenceSnippets.Any())
            {
                warnings.Add($"Rejected candidate '{c.Title}' because evidence snippets are empty.");
                continue;
            }

            valid.Add(c);
        }

        return (valid, warnings);
    }

    private static bool ContainsCollaborationSignal(string value) =>
        value.Contains("collab", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("コラボ", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("special", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("キャンペーン", StringComparison.OrdinalIgnoreCase);
}
