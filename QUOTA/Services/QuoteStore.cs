using System.Text.Json;
using QUOTA.Models;

namespace QUOTA.Services;

public class QuoteStore
{
    private readonly List<Quote> _quotes = new();

    public IReadOnlyList<Quote> Quotes => _quotes;

    public void AddQuote(Quote quote)
    {
        _quotes.Add(quote);
    }

    public Quote? GetRandomQuote()
    {
        if (_quotes.Count == 0)
        {
            return null;
        }

        var index = Random.Shared.Next(_quotes.Count);
        return _quotes[index];
    }

    public List<Quote> GetQuotesByGenre(string genre)
    {
        return _quotes
            .Where(q => q.Genre.Equals(genre, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public async Task LoadFromJsonAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return;
        }

        await using var stream = File.OpenRead(filePath);
        var quotes = await JsonSerializer.DeserializeAsync<List<Quote>>(stream);
        _quotes.Clear();
        if (quotes is not null)
        {
            _quotes.AddRange(quotes);
        }
    }

    public async Task SaveToJsonAsync(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, _quotes, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }
}
