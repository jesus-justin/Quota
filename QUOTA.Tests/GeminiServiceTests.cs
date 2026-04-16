using System.Net;
using System.Text;
using System.Text.Json;
using QUOTA.Services;
using Xunit;

namespace QUOTA.Tests;

public class GeminiServiceTests
{
    [Fact]
    public async Task AnalyzeQuoteAsync_ReturnsFallbackWhenNotConfigured()
    {
        using var client = new HttpClient(new ThrowingHandler());
        var service = new GeminiService(client, null);

        var analysis = await service.AnalyzeQuoteAsync("Keep going.");

        Assert.Equal("General", analysis.Genre);
        Assert.Contains("youtube.com/results", analysis.MusicSearchUrl);
    }

    [Fact]
    public async Task AnalyzeQuoteAsync_ParsesJsonFromModelText()
    {
        var response = "Some extra text before JSON {\"genre\":\"Focus\",\"musicSearchUrl\":\"https://www.youtube.com/results?search_query=focus+music\"} after text";
        using var client = new HttpClient(new StaticResponseHandler(CreateGeminiResponse(response)));
        var service = new GeminiService(client, "fake-key");

        var analysis = await service.AnalyzeQuoteAsync("Stay focused.");

        Assert.Equal("Focus", analysis.Genre);
        Assert.Equal("https://www.youtube.com/results?search_query=focus+music", analysis.MusicSearchUrl);
    }

    [Fact]
    public void ResolveApiKey_ReadsEnvironmentFile()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"quota-{Guid.NewGuid():N}.env");
        File.WriteAllText(tempFile, "GOOGLE_API_KEY=abc123");

        try
        {
            var apiKey = GeminiService.ResolveApiKey(tempFile);

            Assert.Equal("abc123", apiKey);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    private static HttpResponseMessage CreateGeminiResponse(string text)
    {
                var payload = new
                {
                        candidates = new[]
                        {
                                new
                                {
                                        content = new
                                        {
                                                parts = new[]
                                                {
                                                        new { text }
                                                }
                                        }
                                }
                        }
                };

                var json = JsonSerializer.Serialize(payload);

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Fallback path should not call HTTP when the service is unconfigured.");
        }
    }

    private sealed class StaticResponseHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;

        public StaticResponseHandler(HttpResponseMessage response)
        {
            _response = response;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_response);
        }
    }
}
