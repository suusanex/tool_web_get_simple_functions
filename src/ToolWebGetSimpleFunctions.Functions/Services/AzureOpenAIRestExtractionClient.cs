using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ToolWebGetSimpleFunctions.Functions.Models.Extraction;
using ToolWebGetSimpleFunctions.Functions.Models.Source;
using ToolWebGetSimpleFunctions.Functions.Options;

namespace ToolWebGetSimpleFunctions.Functions.Services;

public sealed class AzureOpenAIRestExtractionClient : IAzureOpenAIExtractionClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AzureOpenAIOptions _options;
    private readonly ILogger<AzureOpenAIRestExtractionClient> _logger;

    public AzureOpenAIRestExtractionClient(
        IHttpClientFactory httpClientFactory,
        IOptions<AzureOpenAIOptions> options,
        ILogger<AzureOpenAIRestExtractionClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ExtractionResult> ExtractAsync(ExtractionInput input, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.Endpoint) || string.IsNullOrWhiteSpace(_options.DeploymentName) || string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException("Azure OpenAI configuration is missing.");
        }

        try
        {
            var client = _httpClientFactory.CreateClient("aoai");
            var uri = $"{_options.Endpoint.TrimEnd('/')}/openai/deployments/{_options.DeploymentName}/chat/completions?api-version={_options.ApiVersion}";
            using var req = new HttpRequestMessage(HttpMethod.Post, uri);
            req.Headers.Add("api-key", _options.ApiKey);
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var body = BuildRequestBody(input);
            req.Content = new StringContent(body, Encoding.UTF8, "application/json");

            var res = await client.SendAsync(req, cancellationToken).ConfigureAwait(false);
            res.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
            var content = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new InvalidOperationException("AOAI response content was empty.");
            }

            var result = JsonSerializer.Deserialize<ExtractionResult>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return result ?? throw new InvalidOperationException("Failed to parse extraction result.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AOAI extraction failed: {ExceptionText}", ex.ToString());
            throw;
        }
    }

    private string BuildRequestBody(ExtractionInput input)
    {
        var compactInput = JsonSerializer.Serialize(input);
        if (compactInput.Length > _options.MaxInputCharacters)
        {
            compactInput = compactInput[.._options.MaxInputCharacters];
        }

        var payload = new
        {
            temperature = _options.Temperature,
            max_tokens = _options.MaxTokens,
            response_format = new
            {
                type = "json_schema",
                json_schema = new
                {
                    name = "rakuspa_event_extraction_result",
                    strict = true,
                    schema = BuildSchema()
                }
            },
            messages = new object[]
            {
                new { role = "system", content = "Extract collaboration events for RAKU SPA 1010 Kanda using only provided evidence." },
                new { role = "user", content = compactInput }
            }
        };

        return JsonSerializer.Serialize(payload);
    }

    private static object BuildSchema()
    {
        return new
        {
            type = "object",
            additionalProperties = false,
            required = new[] { "candidates", "warnings" },
            properties = new
            {
                candidates = new
                {
                    type = "array",
                    items = new
                    {
                        type = "object",
                        additionalProperties = false,
                        required = new[] { "title", "startDate", "endDate", "venueConfirmed", "venueEvidence", "eventType", "sourceUrls", "evidenceSnippets", "confidence", "warnings" },
                        properties = new
                        {
                            title = new { type = "string" },
                            startDate = new { type = "string" },
                            endDate = new { type = "string" },
                            venueConfirmed = new { type = "boolean" },
                            venueEvidence = new { type = "string" },
                            eventType = new { type = "string" },
                            sourceUrls = new { type = "array", items = new { type = "string" } },
                            evidenceSnippets = new { type = "array", items = new { type = "string" } },
                            confidence = new { type = "string", @enum = new[] { "high", "medium", "low" } },
                            warnings = new { type = "array", items = new { type = "string" } }
                        }
                    }
                },
                warnings = new
                {
                    type = "array",
                    items = new { type = "string" }
                }
            }
        };
    }
}
