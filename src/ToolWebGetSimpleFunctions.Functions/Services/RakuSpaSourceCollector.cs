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
    private static readonly Regex DateLikeRegex = new(@"\d{4}[/-]\d{1,2}[/-]\d{1,2}", RegexOptions.Compiled);

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

            var candidateLinks = official.Links
                .Where(link => IsLikelyCandidateLink(link))
                .Take(_options.MaxCandidateArticles)
                .ToList();

            var documents = new List<SourceDocument> { official };

            foreach (var link in candidateLinks)
            {
                var article = await FetchDocumentAsync(link, "news_article", cancellationToken).ConfigureAwait(false);
                fetchedUrls.Add(article.Url);

                if (!HasSignal(article))
                {
                    continue;
                }

                documents.Add(article);

                foreach (var campaignLink in article.Links.Take(_options.MaxLinkedPagesPerRequest))
                {
                    if (!IsCampaignLikeLink(campaignLink))
                    {
                        continue;
                    }

                    var campaign = await FetchDocumentAsync(campaignLink, "campaign_page", cancellationToken).ConfigureAwait(false);
                    fetchedUrls.Add(campaign.Url);
                    documents.Add(campaign);
                }
            }

            var input = new ExtractionInput
            {
                SearchDate = searchDate.ToString("yyyy-MM-dd"),
                FacilityName = _options.FacilityName,
                FacilityAliases = _options.FacilityAliases.ToList(),
                Documents = documents,
                DetectedDateLikeStrings = documents.SelectMany(d => DateLikeRegex.Matches(d.Text).Select(m => m.Value)).Distinct().ToList(),
                DetectedVenueLikeStrings = documents
                    .SelectMany(d => _options.FacilityAliases.Where(alias => ContainsIgnoreCase(d.Text, alias)))
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

        var html = response.Content.Headers.ContentType?.CharSet is { Length: > 0 } charset
            ? System.Text.Encoding.GetEncoding(charset).GetString(bytes)
            : System.Text.Encoding.UTF8.GetString(bytes);

        return await _extractor.ExtractAsync(html, url, sourceType, cancellationToken).ConfigureAwait(false);
    }

    private static bool IsLikelyCandidateLink(string link) => ContainsIgnoreCase(link, "news") || ContainsIgnoreCase(link, "event") || ContainsIgnoreCase(link, "collabo");
    private static bool IsCampaignLikeLink(string link) => ContainsIgnoreCase(link, "rakuspa.com") || ContainsIgnoreCase(link, "prtimes.jp") || ContainsIgnoreCase(link, "campaign");
    private static bool HasSignal(SourceDocument doc) => Signals.Any(s => ContainsIgnoreCase(doc.Title, s) || ContainsIgnoreCase(doc.Text, s));
    private static bool ContainsIgnoreCase(string value, string keyword) => value.Contains(keyword, StringComparison.OrdinalIgnoreCase);
}
