using System.Text.Json;
using QUOTA.Models;

namespace QUOTA.Services;

public class QuoteStore
{
    private readonly List<Quote> _quotes = new();
    private readonly object _sync = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public IReadOnlyList<Quote> Quotes
    {
        get
        {
            lock (_sync)
            {
                return _quotes.Select(CloneQuote).ToArray();
            }
        }
    }

    public void AddQuote(Quote quote)
    {
        ArgumentNullException.ThrowIfNull(quote);
        ValidateQuote(quote);

        lock (_sync)
        {
            _quotes.Add(CloneQuote(quote));
        }
    }

    public Quote? GetRandomQuote()
    {
        lock (_sync)
        {
            if (_quotes.Count == 0)
            {
                return null;
            }

            var index = Random.Shared.Next(_quotes.Count);
            return CloneQuote(_quotes[index]);
        }
    }

    public List<Quote> GetQuotesByGenre(string genre)
    {
        if (string.IsNullOrWhiteSpace(genre))
        {
            return [];
        }

        var normalizedGenre = genre.Trim();

        lock (_sync)
        {
            return _quotes
                .Where(q => q.Genre.Equals(normalizedGenre, StringComparison.OrdinalIgnoreCase))
                .Select(CloneQuote)
                .ToList();
        }
    }

    public async Task LoadFromJsonAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return;
        }

        await using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var quotes = await JsonSerializer.DeserializeAsync<List<Quote>>(stream, JsonOptions);

        if (quotes is null)
        {
            return;
        }

        var normalized = quotes
            .Where(IsValidQuote)
            .Select(CloneQuote)
            .ToList();

        lock (_sync)
        {
            _quotes.Clear();
            _quotes.AddRange(normalized);
        }
    }

    public async Task SaveToJsonAsync(string filePath)
    {
        List<Quote> snapshot;

        lock (_sync)
        {
            snapshot = _quotes.Select(CloneQuote).ToList();
        }

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempDirectory = string.IsNullOrWhiteSpace(directory) ? Path.GetTempPath() : directory;
        var tempFilePath = Path.Combine(tempDirectory, $"{Path.GetFileName(filePath)}.{Guid.NewGuid():N}.tmp");

        await using (var stream = File.Create(tempFilePath))
        {
            await JsonSerializer.SerializeAsync(stream, snapshot, JsonOptions);
        }

        if (File.Exists(filePath))
        {
            File.Replace(tempFilePath, filePath, null);
        }
        else
        {
            File.Move(tempFilePath, filePath);
        }
    }

    private static bool IsValidQuote(Quote quote)
    {
        return quote is not null &&
               !string.IsNullOrWhiteSpace(quote.Text) &&
               !string.IsNullOrWhiteSpace(quote.Author) &&
               !string.IsNullOrWhiteSpace(quote.Genre) &&
               !string.IsNullOrWhiteSpace(quote.MusicUrl);
    }

    private static void ValidateQuote(Quote quote)
    {
        if (string.IsNullOrWhiteSpace(quote.Text))
        {
            throw new ArgumentException("Quote text is required.", nameof(quote));
        }

        quote.Text = quote.Text.Trim();
        quote.Author = string.IsNullOrWhiteSpace(quote.Author) ? "Unknown" : quote.Author.Trim();
        quote.Genre = string.IsNullOrWhiteSpace(quote.Genre) ? "General" : quote.Genre.Trim();
        quote.MusicUrl = string.IsNullOrWhiteSpace(quote.MusicUrl) ? string.Empty : quote.MusicUrl.Trim();
    }

    private static Quote CloneQuote(Quote quote)
    {
        return new Quote
        {
            Id = quote.Id,
            Text = quote.Text,
            Author = quote.Author,
            Genre = quote.Genre,
            MusicUrl = quote.MusicUrl
        };
    }
}
