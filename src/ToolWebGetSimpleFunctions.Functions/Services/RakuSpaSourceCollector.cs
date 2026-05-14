using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ToolWebGetSimpleFunctions.Functions.Models.Api;
using ToolWebGetSimpleFunctions.Functions.Models.Source;
using ToolWebGetSimpleFunctions.Functions.Options;

namespace ToolWebGetSimpleFunctions.Functions.Services;

public sealed class RakuSpaSourceCollector : ISourceCollector
{
    private static readonly string[] Signals = ["コラボ", "開催決定", "スペシャルイベント", "×極楽湯", "×RAKU SPA"];
    private static readonly string[] CampaignPathKeywords = ["campaign", "special", "press", "collabo", "collaboration", "event"];
    private static readonly string[] CampaignTextSignals = ["コラボ", "開催決定", "スペシャルイベント", "キャンペーン", "特設", "詳細", "PR TIMES"];
    private static readonly Regex DateLikeRegex = new(@"(?:\d{4}[/-]\d{1,2}[/-]\d{1,2}|\d{4}年\d{1,2}月\d{1,2}日|\d{1,2}月\d{1,2}日(?:\s*[〜～~\-−]\s*(?:\d{1,2}月)?\d{1,2}日)?)", RegexOptions.Compiled);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IHtmlTextExtractor _extractor;
    private readonly SourceCollectionOptions _options;
    private readonly ILogger<RakuSpaSourceCollector> _logger;

    public RakuSpaSourceCollector(
        IHttpClientFactory httpClientFactory,
        IHtmlTextExtractor extractor,
        IOptions<SourceCollectionOptions> options,
        ILogger<RakuSpaSourceCollector> logger)
    {
        _httpClientFactory = httpClientFactory;
        _extractor = extractor;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<(ExtractionInput Input, SourceSummaryDto Summary)> CollectAsync(DateOnly searchDate, CancellationToken cancellationToken)
    {
        var fetchedUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var official = await FetchDocumentAsync(_options.OfficialNewsUrl, "official_news", cancellationToken).ConfigureAwait(false);
            fetchedUrls.Add(official.Url);

            var candidateLinks = official.LinkDetails
                .Where(link => IsLikelyCandidateLink(link))
                .Select(link => link.Url)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(_options.MaxCandidateArticles)
                .ToList();

            var documents = new List<SourceDocument> { official };

            foreach (var link in candidateLinks)
            {
                if (!fetchedUrls.Add(link))
                {
                    continue;
                }

                var article = await FetchDocumentAsync(link, "news_article", cancellationToken).ConfigureAwait(false);

                if (!HasSignal(article))
                {
                    continue;
                }

                documents.Add(article);

                foreach (var campaignLink in article.LinkDetails
                    .Where(IsCampaignLikeLink)
                    .Select(linkDetail => linkDetail.Url)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(_options.MaxLinkedPagesPerRequest))
                {
                    if (!fetchedUrls.Add(campaignLink))
                    {
                        continue;
                    }

                    var campaign = await FetchDocumentAsync(campaignLink, "campaign_page", cancellationToken).ConfigureAwait(false);
                    documents.Add(campaign);
                }
            }

            var input = new ExtractionInput
            {
                SearchDate = searchDate.ToString("yyyy-MM-dd"),
                FacilityName = _options.FacilityName,
                FacilityAliases = _options.FacilityAliases.ToList(),
                Documents = documents,
                DetectedDateLikeStrings = documents.SelectMany(ExtractDateLikeStrings).Distinct().ToList(),
                DetectedVenueLikeStrings = documents
                    .SelectMany(d => EnumerateTextFragments(d).SelectMany(text => _options.FacilityAliases.Where(alias => ContainsIgnoreCase(text, alias))))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList()
            };

            var summary = new SourceSummaryDto
            {
                OfficialNewsUrl = _options.OfficialNewsUrl,
                FetchedUrls = fetchedUrls.ToList(),
                CandidateArticleCount = candidateLinks.Count
            };

            return (input, summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Source collection failed: {ExceptionText}", ex.ToString());
            throw;
        }
    }

    private async Task<SourceDocument> FetchDocumentAsync(string url, string sourceType, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("source");
        var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        if (bytes.Length > _options.MaxPageBytes)
        {
            throw new InvalidOperationException($"Page too large: {url}");
        }

        var html = DecodeContent(bytes, response.Content.Headers.ContentType?.CharSet);

        return await _extractor.ExtractAsync(html, url, sourceType, cancellationToken).ConfigureAwait(false);
    }

    private static IEnumerable<string> ExtractDateLikeStrings(SourceDocument document)
        => EnumerateTextFragments(document)
            .SelectMany(text => DateLikeRegex.Matches(text).Select(match => match.Value));

    private static IEnumerable<string> EnumerateTextFragments(SourceDocument document)
    {
        yield return document.Title;
        yield return document.Text;

        foreach (var imageAltText in document.ImageAltTexts)
        {
            yield return imageAltText;
        }
    }

    private static string DecodeContent(byte[] bytes, string? charset)
    {
        if (string.IsNullOrWhiteSpace(charset))
        {
            return System.Text.Encoding.UTF8.GetString(bytes);
        }

        var normalizedCharset = charset.Trim().Trim('"', '\'');
        return System.Text.Encoding.GetEncoding(normalizedCharset).GetString(bytes);
    }

    private static bool IsLikelyCandidateLink(SourceLink link)
        => ContainsIgnoreCase(link.Url, "news")
            || ContainsIgnoreCase(link.Url, "event")
            || ContainsIgnoreCase(link.Url, "collabo")
            || Signals.Any(signal => ContainsIgnoreCase(link.Text, signal));

    private static bool IsCampaignLikeLink(SourceLink link)
    {
        if (!Uri.TryCreate(link.Url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (ContainsIgnoreCase(uri.Host, "prtimes.jp"))
        {
            return true;
        }

        if (!ContainsIgnoreCase(uri.Host, "rakuspa.com"))
        {
            return false;
        }

        return CampaignPathKeywords.Any(keyword => ContainsIgnoreCase(uri.AbsolutePath, keyword) || ContainsIgnoreCase(uri.Query, keyword))
            || CampaignTextSignals.Any(signal => ContainsIgnoreCase(link.Text, signal));
    }

    private static bool HasSignal(SourceDocument doc) => Signals.Any(s => ContainsIgnoreCase(doc.Title, s) || ContainsIgnoreCase(doc.Text, s));
    private static bool ContainsIgnoreCase(string value, string keyword) => value.Contains(keyword, StringComparison.OrdinalIgnoreCase);
}
