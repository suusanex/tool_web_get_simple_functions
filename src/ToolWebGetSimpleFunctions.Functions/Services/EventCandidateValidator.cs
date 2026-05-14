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
        var combinedTextCorpus = BuildCombinedTextCorpus(input);
        var venueTokens = BuildVenueTokens(input);

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

            if (string.IsNullOrWhiteSpace(c.VenueEvidence) || !ContainsAnyToken(c.VenueEvidence, venueTokens) || !ContainsTraceableText(c.VenueEvidence, combinedTextCorpus))
            {
                warnings.Add($"Rejected candidate '{c.Title}' because venue evidence is not traceable.");
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

            if (!ContainsTraceableText(c.Title, combinedTextCorpus))
            {
                warnings.Add($"Rejected candidate '{c.Title}' because title is not traceable to input text.");
                continue;
            }

            if (!IsDateTraceable(c.StartDate, input) || !IsDateTraceable(c.EndDate, input))
            {
                warnings.Add($"Rejected candidate '{c.Title}' because date values are not traceable.");
                continue;
            }

            if (c.EvidenceSnippets.Any(snippet => !ContainsTraceableText(snippet, combinedTextCorpus)))
            {
                warnings.Add($"Rejected candidate '{c.Title}' because one or more evidence snippets are not traceable.");
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

    private static string BuildCombinedTextCorpus(ExtractionInput input)
    {
        var parts = new List<string>();
        parts.AddRange(input.Documents.Select(d => d.Title));
        parts.AddRange(input.Documents.Select(d => d.Text));
        parts.AddRange(input.Documents.SelectMany(d => d.ImageAltTexts));
        parts.AddRange(input.DetectedDateLikeStrings);
        parts.AddRange(input.DetectedVenueLikeStrings);
        parts.AddRange(input.FacilityAliases);
        parts.Add(input.FacilityName);
        return string.Join('\n', parts.Where(p => !string.IsNullOrWhiteSpace(p)));
    }

    private static HashSet<string> BuildVenueTokens(ExtractionInput input)
    {
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var alias in input.FacilityAliases)
        {
            tokens.Add(alias);
            tokens.Add(RemoveSpaces(alias));
        }

        foreach (var venue in input.DetectedVenueLikeStrings)
        {
            tokens.Add(venue);
            tokens.Add(RemoveSpaces(venue));
        }

        tokens.Add(input.FacilityName);
        tokens.Add(RemoveSpaces(input.FacilityName));
        return tokens;
    }

    private static bool IsDateTraceable(string dateText, ExtractionInput input)
    {
        var candidateTokens = BuildDateTokens(dateText);
        if (candidateTokens.Count == 0)
        {
            return false;
        }

        if (input.DetectedDateLikeStrings.Any(source => candidateTokens.Any(token => ContainsIgnoreCase(source, token))))
        {
            return true;
        }

        return input.Documents.Any(d =>
            candidateTokens.Any(token =>
                ContainsIgnoreCase(d.Title, token)
                || ContainsIgnoreCase(d.Text, token)
                || d.ImageAltTexts.Any(imageAltText => ContainsIgnoreCase(imageAltText, token))));
    }

    private static HashSet<string> BuildDateTokens(string dateText)
    {
        var tokens = new HashSet<string>(StringComparer.Ordinal);
        if (!DateOnly.TryParseExact(dateText, "yyyy-MM-dd", out var parsedDate))
        {
            return tokens;
        }

        tokens.Add(dateText);
        tokens.Add($"{parsedDate.Year:D4}/{parsedDate.Month}/{parsedDate.Day}");
        tokens.Add($"{parsedDate.Year:D4}/{parsedDate.Month:D2}/{parsedDate.Day:D2}");
        tokens.Add($"{parsedDate.Year:D4}年{parsedDate.Month}月{parsedDate.Day}日");
        tokens.Add($"{parsedDate.Year:D4}年{parsedDate.Month:D2}月{parsedDate.Day:D2}日");
        tokens.Add($"{parsedDate.Month}/{parsedDate.Day}");
        tokens.Add($"{parsedDate.Month:D2}/{parsedDate.Day:D2}");
        tokens.Add($"{parsedDate.Month}月{parsedDate.Day}日");
        tokens.Add($"{parsedDate.Month:D2}月{parsedDate.Day:D2}日");
        return tokens;
    }

    private static bool ContainsTraceableText(string value, string corpus)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (ContainsIgnoreCase(corpus, value))
        {
            return true;
        }

        var compactValue = RemoveSpaces(value);
        var compactCorpus = RemoveSpaces(corpus);
        return ContainsIgnoreCase(compactCorpus, compactValue);
    }

    private static bool ContainsAnyToken(string value, IEnumerable<string> tokens)
        => tokens.Any(token => !string.IsNullOrWhiteSpace(token) && (ContainsIgnoreCase(value, token) || ContainsIgnoreCase(RemoveSpaces(value), RemoveSpaces(token))));

    private static string RemoveSpaces(string value)
        => value.Replace(" ", string.Empty, StringComparison.Ordinal).Replace("　", string.Empty, StringComparison.Ordinal);

    private static bool ContainsIgnoreCase(string source, string value)
        => source.Contains(value, StringComparison.OrdinalIgnoreCase);
}
