using System.Text.Json;
using QUOTA.Models;
using QUOTA.Services;
using Xunit;

namespace QUOTA.Tests;

public class QuoteStoreTests
{
    [Fact]
    public void AddQuote_RejectsEmptyText()
    {
        var store = new QuoteStore();

        var exception = Assert.Throws<ArgumentException>(() => store.AddQuote(new Quote
        {
            Text = "   ",
            Author = "Someone",
            Genre = "Focus",
            MusicUrl = "https://example.com"
        }));

        Assert.Contains("Quote text is required", exception.Message);
    }

    [Fact]
    public void AddQuote_TrimsAndSnapshotsData()
    {
        var store = new QuoteStore();
        var quote = new Quote
        {
            Text = "  Stay curious  ",
            Author = "  Ada Lovelace  ",
            Genre = "  Focus  ",
            MusicUrl = "  https://example.com  "
        };

        store.AddQuote(quote);
        var stored = store.Quotes.Single();

        Assert.Equal("Stay curious", stored.Text);
        Assert.Equal("Ada Lovelace", stored.Author);
        Assert.Equal("Focus", stored.Genre);
        Assert.Equal("https://example.com", stored.MusicUrl);
    }

    [Fact]
    public void GetQuotesByGenre_IsCaseInsensitive()
    {
        var store = new QuoteStore();
        store.AddQuote(new Quote
        {
            Text = "First",
            Author = "A",
            Genre = "Focus",
            MusicUrl = "https://example.com/1"
        });
        store.AddQuote(new Quote
        {
            Text = "Second",
            Author = "B",
            Genre = "Calm",
            MusicUrl = "https://example.com/2"
        });

        var results = store.GetQuotesByGenre("fOcUs");

        Assert.Single(results);
        Assert.Equal("First", results[0].Text);
    }

    [Fact]
    public async Task LoadAndSaveJson_RoundTripsQuotes()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "quota-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        var filePath = Path.Combine(tempDirectory, "quotes.json");
        var expected = new[]
        {
            new Quote
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Text = "Persist me",
                Author = "Tester",
                Genre = "General",
                MusicUrl = "https://example.com/music"
            }
        };

        await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(expected));

        var store = new QuoteStore();
        await store.LoadFromJsonAsync(filePath);
        await store.SaveToJsonAsync(filePath);

        var reloaded = new QuoteStore();
        await reloaded.LoadFromJsonAsync(filePath);

        var quote = Assert.Single(reloaded.Quotes);
        Assert.Equal("Persist me", quote.Text);
        Assert.Equal("Tester", quote.Author);
        Assert.Equal("General", quote.Genre);
        Assert.Equal("https://example.com/music", quote.MusicUrl);
    }
}
