using System.Net.Http.Json;
using System.Text.Json;

namespace QUOTA.Services;

public record QuoteAnalysis(string Genre, string MusicSearchUrl);

public class GeminiService
{
    private static readonly string[] ModelFallbackOrder =
    {
        "gemini-2.5-flash",
        "gemini-2.0-flash",
        "gemini-1.5-flash"
    };

    private const string EndpointTemplate = "https://generativelanguage.googleapis.com/v1beta/models/{0}:generateContent?key={1}";
    private readonly HttpClient _httpClient;
    private readonly string? _apiKey;

    public GeminiService(HttpClient httpClient, string? apiKey)
    {
        _httpClient = httpClient;
        _apiKey = apiKey;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

    public async Task<QuoteAnalysis> AnalyzeQuoteAsync(string quoteText)
    {
        if (!IsConfigured)
        {
            return new QuoteAnalysis("General", BuildMusicSearchUrl("general motivational instrumental music"));
        }

        var prompt = $@"Analyze this quote and infer the mood/genre.
    Quote: {quoteText}

    Return only compact JSON in this exact shape:
    {{""genre"":"""",""musicSearchUrl"":""""}}

    Rules:
    - genre should be one word or short phrase like Life, Death, Jogging, Focus.
    - musicSearchUrl must be a valid https YouTube search URL.
    - Do not include markdown or extra text.";

        var request = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[]
                    {
                        new { text = prompt }
                    }
                }
            }
        };

        var rawResponse = await PostWithModelFallbackAsync(request);
        var modelText = ExtractModelText(rawResponse);
        var json = ExtractJsonObject(modelText);

        if (string.IsNullOrWhiteSpace(json))
        {
            return new QuoteAnalysis("General", BuildMusicSearchUrl("ambient mood music"));
        }

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var genre = root.TryGetProperty("genre", out var genreElement)
            ? genreElement.GetString()
            : null;

        var musicUrl = root.TryGetProperty("musicSearchUrl", out var musicElement)
            ? musicElement.GetString()
            : null;

        genre = string.IsNullOrWhiteSpace(genre) ? "General" : genre.Trim();
        musicUrl = string.IsNullOrWhiteSpace(musicUrl)
            ? BuildMusicSearchUrl($"{genre} mood music")
            : musicUrl.Trim();

        return new QuoteAnalysis(genre, musicUrl);
    }

    public static string? ResolveApiKey(string envFilePath)
    {
        var envCandidates = new[] { "Gemini_Key", "GEMINI_API_KEY", "GOOGLE_API_KEY" };
        foreach (var envName in envCandidates)
        {
            var fromEnv = Environment.GetEnvironmentVariable(envName);
            if (!string.IsNullOrWhiteSpace(fromEnv))
            {
                return fromEnv;
            }
        }

        if (!File.Exists(envFilePath))
        {
            return null;
        }

        foreach (var line in File.ReadLines(envFilePath))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            var splitIndex = trimmed.IndexOf('=');
            if (splitIndex <= 0)
            {
                continue;
            }

            var key = trimmed[..splitIndex].Trim();
            var value = trimmed[(splitIndex + 1)..].Trim().Trim('"');

            if (key.Equals("Gemini_Key", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("GEMINI_API_KEY", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("GOOGLE_API_KEY", StringComparison.OrdinalIgnoreCase))
            {
                return string.IsNullOrWhiteSpace(value) ? null : value;
            }
        }

        return null;
    }

    private async Task<string> PostWithModelFallbackAsync(object request)
    {
        var lastError = string.Empty;

        foreach (var model in ModelFallbackOrder)
        {
            var endpoint = string.Format(EndpointTemplate, model, _apiKey);
            using var response = await _httpClient.PostAsJsonAsync(endpoint, request);
            var content = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                return content;
            }

            if ((int)response.StatusCode is 404 or 400)
            {
                lastError = content;
                continue;
            }

            response.EnsureSuccessStatusCode();
        }

        throw new InvalidOperationException($"Gemini API failed for all configured models. Last response: {lastError}");
    }

    private static string ExtractModelText(string rawResponse)
    {
        using var doc = JsonDocument.Parse(rawResponse);
        var root = doc.RootElement;

        if (root.TryGetProperty("candidates", out var candidates) && candidates.ValueKind == JsonValueKind.Array)
        {
            var firstCandidate = candidates.EnumerateArray().FirstOrDefault();
            if (firstCandidate.ValueKind != JsonValueKind.Undefined &&
                firstCandidate.TryGetProperty("content", out var content) &&
                content.TryGetProperty("parts", out var parts) &&
                parts.ValueKind == JsonValueKind.Array)
            {
                var firstPart = parts.EnumerateArray().FirstOrDefault();
                if (firstPart.ValueKind != JsonValueKind.Undefined &&
                    firstPart.TryGetProperty("text", out var textElement))
                {
                    return textElement.GetString() ?? string.Empty;
                }
            }
        }

        return string.Empty;
    }

    private static string ExtractJsonObject(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var first = text.IndexOf('{');
        var last = text.LastIndexOf('}');
        if (first < 0 || last <= first)
        {
            return string.Empty;
        }

        return text[first..(last + 1)];
    }

    private static string BuildMusicSearchUrl(string query)
    {
        return $"https://www.youtube.com/results?search_query={Uri.EscapeDataString(query)}";
    }
}
