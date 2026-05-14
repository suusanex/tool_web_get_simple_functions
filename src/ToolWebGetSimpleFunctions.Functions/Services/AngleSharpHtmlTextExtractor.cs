using AngleSharp.Html.Parser;
using ToolWebGetSimpleFunctions.Functions.Models.Source;

namespace ToolWebGetSimpleFunctions.Functions.Services;

public sealed class AngleSharpHtmlTextExtractor : IHtmlTextExtractor
{
    public async Task<SourceDocument> ExtractAsync(string html, string url, string sourceType, CancellationToken cancellationToken)
    {
        var parser = new HtmlParser();
        var doc = await parser.ParseDocumentAsync(html, cancellationToken).ConfigureAwait(false);

        var title = doc.Title ?? string.Empty;
        var text = doc.Body?.TextContent?.Trim() ?? string.Empty;

        var linkDetails = doc.QuerySelectorAll("a[href]")
            .Select(node => new SourceLink
            {
                Url = ToAbsoluteUrl(url, node.GetAttribute("href")!),
                Text = node.TextContent?.Trim() ?? string.Empty
            })
            .GroupBy(link => link.Url, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(link => link.Text.Length).First())
            .ToList();

        var links = linkDetails
            .Select(link => link.Url)
            .ToList();

        var imageAltTexts = doc.QuerySelectorAll("img[alt]")
            .Select(node => node.GetAttribute("alt"))
            .Where(alt => !string.IsNullOrWhiteSpace(alt))
            .Select(alt => alt!.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return new SourceDocument
        {
            Url = url,
            SourceType = sourceType,
            Title = title,
            Text = text,
            LinkDetails = linkDetails,
            Links = links,
            ImageAltTexts = imageAltTexts
        };
    }

    private static string ToAbsoluteUrl(string baseUrl, string href)
    {
        if (Uri.TryCreate(href, UriKind.Absolute, out var absolute)
            && (absolute.Scheme == Uri.UriSchemeHttp || absolute.Scheme == Uri.UriSchemeHttps))
        {
            return absolute.ToString();
        }

        var baseUri = new Uri(baseUrl, UriKind.Absolute);
        return new Uri(baseUri, href).ToString();
    }
}
