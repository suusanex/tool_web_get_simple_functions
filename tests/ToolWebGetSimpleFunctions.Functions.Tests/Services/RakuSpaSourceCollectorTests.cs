using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ToolWebGetSimpleFunctions.Functions.Options;
using ToolWebGetSimpleFunctions.Functions.Services;
using Xunit;

namespace ToolWebGetSimpleFunctions.Functions.Tests.Services;

public sealed class RakuSpaSourceCollectorTests
{
    [Fact]
    public async Task CollectAsync_FollowsVisibleSignalLinksAndCampaignLinksAfterFilteringAsync()
    {
        var officialUrl = "https://rakuspa.com/kanda/news/";
        var articleUrl = "https://rakuspa.com/kanda/topics/123";
        var campaignUrl = "https://rakuspa.com/kanda/special/collaboration";
        var handler = new StubHttpMessageHandler((request, _) => request.RequestUri!.ToString().TrimEnd('/') switch
        {
            "https://rakuspa.com/kanda/news" => HtmlResponse(
                """
                <html><head><title>お知らせ</title></head><body>
                <a href="/kanda/topics/123">人気作品コラボ開催決定</a>
                <a href="/kanda/access">アクセス</a>
                </body></html>
                """),
            "https://rakuspa.com/kanda/topics/123" => HtmlResponse(
                """
                <html><head><title>人気作品コラボ開催決定</title></head><body>
                <p>2026年5月28日から開催します。</p>
                <a href="/kanda/access">アクセス</a>
                <a href="/kanda/floor">館内案内</a>
                <a href="/kanda/faq">FAQ</a>
                <a href="/kanda/special/collaboration">詳細はこちら</a>
                </body></html>
                """),
            "https://rakuspa.com/kanda/special/collaboration" => HtmlResponse(
                """
                <html><head><title>特設ページ</title></head><body>
                <img alt="6月30日まで開催" src="/banner.jpg" />
                <p>RAKU SPA 1010 神田で開催</p>
                </body></html>
                """),
            _ => throw new Xunit.Sdk.XunitException($"Unexpected URL: {request.RequestUri}")
        });

        var collector = CreateCollector(handler, new SourceCollectionOptions
        {
            OfficialNewsUrl = officialUrl,
            MaxCandidateArticles = 5,
            MaxLinkedPagesPerRequest = 1
        });

        var (input, summary) = await collector.CollectAsync(new DateOnly(2026, 5, 29), CancellationToken.None);

        Assert.Equal(3, input.Documents.Count);
        Assert.Contains(input.Documents, d => d.Url == articleUrl && d.SourceType == "news_article");
        Assert.Contains(input.Documents, d => d.Url == campaignUrl && d.SourceType == "campaign_page");
        Assert.Contains("2026年5月28日", input.DetectedDateLikeStrings);
        Assert.Contains("6月30日", input.DetectedDateLikeStrings);
        Assert.Contains("RAKU SPA 1010 神田", input.DetectedVenueLikeStrings);
        Assert.DoesNotContain(summary.FetchedUrls, url => url.EndsWith("/access", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CollectAsync_WhenResponseUsesShiftJis_DecodesPageAsync()
    {
        var bytes = Encoding.GetEncoding("shift_jis").GetBytes("""
            <html><head><title>人気作品コラボ開催決定</title></head><body>
            <p>2026年5月28日からRAKU SPA 1010 神田で開催</p>
            </body></html>
            """);
        var handler = new StubHttpMessageHandler((_, _) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(bytes)
            {
                Headers =
                {
                    ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/html")
                    {
                        CharSet = "shift_jis"
                    }
                }
            }
        });

        var collector = CreateCollector(handler, new SourceCollectionOptions
        {
            OfficialNewsUrl = "https://rakuspa.com/kanda/news/",
            MaxCandidateArticles = 0
        });

        var (input, _) = await collector.CollectAsync(new DateOnly(2026, 5, 29), CancellationToken.None);

        Assert.Contains(input.Documents, d => d.Title.Contains("コラボ開催決定", StringComparison.Ordinal));
        Assert.Contains("2026年5月28日", input.DetectedDateLikeStrings);
    }

    [Fact]
    public async Task CollectAsync_WhenPageExceedsLimit_ThrowsAsync()
    {
        var bytes = new byte[32];
        var handler = new StubHttpMessageHandler((_, _) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(bytes)
        });

        var collector = CreateCollector(handler, new SourceCollectionOptions
        {
            OfficialNewsUrl = "https://rakuspa.com/kanda/news/",
            MaxPageBytes = 16
        });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => collector.CollectAsync(new DateOnly(2026, 5, 29), CancellationToken.None));

        Assert.Contains("Page too large", ex.Message, StringComparison.Ordinal);
    }

    private static RakuSpaSourceCollector CreateCollector(HttpMessageHandler handler, SourceCollectionOptions options)
    {
        return new RakuSpaSourceCollector(
            new StubHttpClientFactory(new HttpClient(handler)),
            new AngleSharpHtmlTextExtractor(),
            Microsoft.Extensions.Options.Options.Create(options),
            NullLogger<RakuSpaSourceCollector>.Instance);
    }

    private static HttpResponseMessage HtmlResponse(string html)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(html, Encoding.UTF8, "text/html")
        };

    private sealed class StubHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(send(request, cancellationToken));
    }
}
