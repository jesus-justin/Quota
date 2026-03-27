using System.Text;
using QUOTA.Models;
using QUOTA.Services;

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

var dataPath = Path.Combine(app.Environment.ContentRootPath, "Data", "quotes.json");
var envPath = Path.Combine(app.Environment.ContentRootPath, ".env");

var store = new QuoteStore();
await store.LoadFromJsonAsync(dataPath);

using var httpClient = new HttpClient();
var geminiApiKey = GeminiService.ResolveApiKey(envPath);
var geminiService = new GeminiService(httpClient, geminiApiKey);

app.MapGet("/", (HttpContext context) =>
{
    var html = BuildHomePage(store.Quotes, geminiService.IsConfigured, null);
    return Results.Content(html, "text/html", Encoding.UTF8);
});

app.MapGet("/quote/random", () =>
{
    var quote = store.GetRandomQuote();
    return quote is null ? Results.NotFound(new { message = "No quotes available." }) : Results.Ok(quote);
});

app.MapGet("/quotes", (string? genre) =>
{
    if (string.IsNullOrWhiteSpace(genre))
    {
        return Results.Ok(store.Quotes);
    }

    return Results.Ok(store.GetQuotesByGenre(genre));
});

app.MapPost("/quotes", async (HttpRequest request) =>
{
    var form = await request.ReadFormAsync();
    var text = form["text"].ToString().Trim();
    var author = form["author"].ToString().Trim();

    if (string.IsNullOrWhiteSpace(text))
    {
        var html = BuildHomePage(store.Quotes, geminiService.IsConfigured, "Quote text is required.");
        return Results.Content(html, "text/html", Encoding.UTF8, StatusCodes.Status400BadRequest);
    }

    if (string.IsNullOrWhiteSpace(author))
    {
        author = "Unknown";
    }

    QuoteAnalysis analysis;
    try
    {
        analysis = await geminiService.AnalyzeQuoteAsync(text);
    }
    catch
    {
        analysis = new QuoteAnalysis("General", "https://www.youtube.com/results?search_query=ambient+music");
    }

    var quote = new Quote
    {
        Text = text,
        Author = author,
        Genre = analysis.Genre,
        MusicUrl = analysis.MusicSearchUrl
    };

    store.AddQuote(quote);
    await store.SaveToJsonAsync(dataPath);

    return Results.Redirect("/");
});

app.Urls.Add("http://localhost:5000");
app.Run();

static string BuildHomePage(IReadOnlyList<Quote> quotes, bool geminiConfigured, string? error)
{
    var random = quotes.Count > 0 ? quotes[Random.Shared.Next(quotes.Count)] : null;
    var items = string.Join("", quotes.Select(q => $"""
        <article class="card">
          <p class="text">&ldquo;{System.Net.WebUtility.HtmlEncode(q.Text)}&rdquo;</p>
          <p class="meta">{System.Net.WebUtility.HtmlEncode(q.Author)} · {System.Net.WebUtility.HtmlEncode(q.Genre)}</p>
          <a href="{System.Net.WebUtility.HtmlEncode(q.MusicUrl)}" target="_blank" rel="noopener">Open Music</a>
        </article>
        """));

    var randomBlock = random is null
        ? "<p>No quotes yet.</p>"
        : $"""
          <article class="hero-card">
            <p class="text">&ldquo;{System.Net.WebUtility.HtmlEncode(random.Text)}&rdquo;</p>
            <p class="meta">{System.Net.WebUtility.HtmlEncode(random.Author)} · {System.Net.WebUtility.HtmlEncode(random.Genre)}</p>
            <a href="{System.Net.WebUtility.HtmlEncode(random.MusicUrl)}" target="_blank" rel="noopener">Play Mood Music</a>
          </article>
          """;

    var status = geminiConfigured
        ? "Gemini API: Connected"
        : "Gemini API: Missing key (.env) - using fallback values";

    var errorHtml = string.IsNullOrWhiteSpace(error)
        ? string.Empty
        : $"<p class=\"error\">{System.Net.WebUtility.HtmlEncode(error)}</p>";

    return $$"""
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>QUOTA</title>
  <style>
    :root {
      --bg: #f6f0e8;
      --panel: #fffaf2;
      --ink: #2d1b12;
      --accent: #b5482f;
      --muted: #6e5346;
    }
    * { box-sizing: border-box; }
    body {
      margin: 0;
      color: var(--ink);
      background: radial-gradient(circle at top right, #ffd9b3 0%, transparent 40%),
                  radial-gradient(circle at bottom left, #ffe9c7 0%, transparent 35%),
                  var(--bg);
      font-family: "Segoe UI", Tahoma, Geneva, Verdana, sans-serif;
    }
    .wrap { max-width: 980px; margin: 0 auto; padding: 24px; }
    h1 { margin: 0 0 8px; letter-spacing: 0.1em; }
    .sub { margin: 0 0 20px; color: var(--muted); }
    .status { margin: 0 0 20px; font-weight: 600; }
    .hero-card, .card, form {
      background: var(--panel);
      border: 1px solid #efcfaf;
      border-radius: 14px;
      padding: 16px;
      box-shadow: 0 5px 16px rgba(80, 35, 20, 0.08);
    }
    .text { font-size: 1.12rem; margin: 0 0 8px; }
    .meta { margin: 0 0 8px; color: var(--muted); }
    a { color: var(--accent); text-decoration: none; font-weight: 600; }
    .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(250px, 1fr)); gap: 14px; }
    form { margin: 18px 0; display: grid; gap: 10px; }
    input { padding: 10px; border-radius: 8px; border: 1px solid #d8b79a; font-size: 1rem; }
    button {
      width: fit-content;
      background: var(--accent);
      color: #fff;
      border: 0;
      border-radius: 10px;
      padding: 10px 14px;
      cursor: pointer;
      font-weight: 700;
    }
    .error { color: #8f1d1d; font-weight: 600; }
  </style>
</head>
<body>
  <div class="wrap">
    <h1>QUOTA</h1>
    <p class="sub">Quote + Mood + Music on localhost</p>
    <p class="status">{{status}}</p>
    {{errorHtml}}

    <h2>Daily Quote</h2>
    {{randomBlock}}

    <h2>Add New Quote</h2>
    <form method="post" action="/quotes">
      <input name="text" placeholder="Quote text" required />
      <input name="author" placeholder="Author (optional)" />
      <button type="submit">Analyze + Save</button>
    </form>

    <h2>All Quotes</h2>
    <div class="grid">{{items}}</div>
  </div>
</body>
</html>
""";
}
