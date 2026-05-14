using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ToolWebGetSimpleFunctions.Functions.Models.Source;
using ToolWebGetSimpleFunctions.Functions.Options;
using ToolWebGetSimpleFunctions.Functions.Services;
using Xunit;

namespace ToolWebGetSimpleFunctions.Functions.Tests.Services;

public sealed class AzureOpenAIRestExtractionClientTests
{
    [Fact]
    public async Task ExtractAsync_SendsStructuredOutputsSchemaWithVenueEvidenceAsync()
    {
        string? capturedRequest = null;
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            capturedRequest = request.Content is null ? null : request.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            var responseJson = """
                               {
                                 "choices": [
                                   {
                                     "message": {
                                       "content": "{\"candidates\":[],\"warnings\":[]}"
                                     }
                                   }
                                 ]
                               }
                               """;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            };
        });

        var client = new AzureOpenAIRestExtractionClient(
            new StubHttpClientFactory(new HttpClient(handler)),
            Microsoft.Extensions.Options.Options.Create(new AzureOpenAIOptions
            {
                Endpoint = "https://example.openai.azure.com",
                DeploymentName = "gpt-test",
                ApiVersion = "2024-10-21",
                ApiKey = "dummy",
                MaxInputCharacters = 80000,
                MaxTokens = 2000,
                Temperature = 0
            }),
            NullLogger<AzureOpenAIRestExtractionClient>.Instance);

        await client.ExtractAsync(new ExtractionInput(), CancellationToken.None);

        Assert.NotNull(capturedRequest);
        Assert.Contains("\"type\":\"json_schema\"", capturedRequest, StringComparison.Ordinal);
        Assert.Contains("\"strict\":true", capturedRequest, StringComparison.Ordinal);
        Assert.Contains("\"name\":\"rakuspa_event_extraction_result\"", capturedRequest, StringComparison.Ordinal);
        Assert.Contains("\"venueEvidence\"", capturedRequest, StringComparison.Ordinal);
        Assert.DoesNotContain("\"type\":\"json_object\"", capturedRequest, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExtractAsync_WhenInputExceedsLimit_ThrowsExplicitErrorAsync()
    {
        var client = new AzureOpenAIRestExtractionClient(
            new StubHttpClientFactory(new HttpClient(new StubHttpMessageHandler((_, _) => throw new InvalidOperationException("HTTP should not be called.")))),
            Microsoft.Extensions.Options.Options.Create(new AzureOpenAIOptions
            {
                Endpoint = "https://example.openai.azure.com",
                DeploymentName = "gpt-test",
                ApiVersion = "2024-10-21",
                ApiKey = "dummy",
                MaxInputCharacters = 10,
                MaxTokens = 2000,
                Temperature = 0
            }),
            NullLogger<AzureOpenAIRestExtractionClient>.Instance);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => client.ExtractAsync(
            new ExtractionInput
            {
                FacilityName = new string('a', 32)
            },
            CancellationToken.None));

        Assert.Contains("exceeded the configured character limit", ex.Message, StringComparison.Ordinal);
    }

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
