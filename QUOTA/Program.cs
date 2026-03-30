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

var appUrl = Environment.GetEnvironmentVariable("QUOTA_URL") ?? "http://localhost:5001";
app.Urls.Add(appUrl);
app.Run();

static string BuildHomePage(IReadOnlyList<Quote> quotes, bool geminiConfigured, string? error)
{
    const int bookAutoCloseMs = 60_000;
    var hasQuotes = quotes.Count > 0;
    var items = string.Join("", quotes.Select((q, i) => $"""
        <article class="card" data-text="{System.Net.WebUtility.HtmlEncode(q.Text)}" data-author="{System.Net.WebUtility.HtmlEncode(q.Author)}" data-genre="{System.Net.WebUtility.HtmlEncode(q.Genre)}" style="--delay:{i * 45}ms">
          <p class="text">&ldquo;{System.Net.WebUtility.HtmlEncode(q.Text)}&rdquo;</p>
          <p class="meta"><span>{System.Net.WebUtility.HtmlEncode(q.Author)}</span><span class="dot">•</span><span class="badge">{System.Net.WebUtility.HtmlEncode(q.Genre)}</span></p>
          <div class="card-actions">
            <a class="ghost-link" href="{System.Net.WebUtility.HtmlEncode(q.MusicUrl)}" target="_blank" rel="noopener">Open Music</a>
            <button type="button" class="copy-btn" data-copy="{System.Net.WebUtility.HtmlEncode(q.Text)} - {System.Net.WebUtility.HtmlEncode(q.Author)}">Copy Quote</button>
          </div>
        </article>
        """));

    var genreFilters = string.Join("", quotes
        .Select(q => q.Genre)
        .Where(g => !string.IsNullOrWhiteSpace(g))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(g => g)
        .Select(g => $"<button type=\"button\" class=\"genre-chip\" data-genre-filter=\"{System.Net.WebUtility.HtmlEncode(g)}\">{System.Net.WebUtility.HtmlEncode(g)}</button>"));

    var randomBlock = $"""
        <div class="book-scene">
          <article class="hero-card hero-book is-closed" id="heroBook" data-close-delay="{bookAutoCloseMs}">
            <div class="book-spine" aria-hidden="true"></div>
            <div class="book-cover" aria-hidden="true">
              <p class="cover-kicker">QUOTA</p>
              <p class="cover-title">Mood Journal</p>
              <p class="cover-hint">Open me with New Random Quote</p>
            </div>

            <div class="book-page">
              <p class="hero-kicker">Featured Quote</p>
              <p class="text" id="heroText">Click <strong>New Random Quote</strong> to open the book.</p>
              <p class="meta" id="heroMeta"></p>
              <div class="hero-actions">
                <a id="heroMusic" class="ghost-link" href="#" target="_blank" rel="noopener" hidden>Play Mood Music</a>
                <button type="button" id="newRandomBtn" {(hasQuotes ? string.Empty : "disabled")}>New Random Quote</button>
              </div>
            </div>
          </article>
        </div>
        {(hasQuotes ? string.Empty : "<p class=\"empty-note\">No quotes yet. Add one first, then use New Random Quote.</p>")}
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
  <link rel="preconnect" href="https://fonts.googleapis.com" />
  <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin />
  <link href="https://fonts.googleapis.com/css2?family=Outfit:wght@400;500;600;700;800&family=Fraunces:opsz,wght@9..144,500;9..144,700&display=swap" rel="stylesheet" />
  <style>
    :root {
      --bg: #1a1410;
      --bg-accent: #2d241f;
      --panel: rgba(45, 36, 31, 0.85);
      --panel-strong: #3d3228;
      --ink: #f5e6d3;
      --accent: #d4a574;
      --accent-dark: #8b6f47;
      --accent-warm: #e8924a;
      --muted: #b8a895;
      --line: #5a4a3f;
      --shadow: 0 16px 40px rgba(0, 0, 0, 0.4);
      --book-spine: #8b4513;
      --page-color: #f9f5f0;
    }
    * { box-sizing: border-box; }
    html { scroll-behavior: smooth; }
    body {
      margin: 0;
      color: var(--ink);
      background: linear-gradient(135deg, #1a1410 0%, #2d1810 50%, #1a1410 100%);
      font-family: "Outfit", "Segoe UI", Tahoma, Geneva, Verdana, sans-serif;
      line-height: 1.55;
      position: relative;
    }
    body::before {
      content: '';
      position: fixed;
      inset: 0;
      background-image:
        repeating-linear-gradient(90deg, transparent, transparent 2px, rgba(212, 165, 116, 0.03) 2px, rgba(212, 165, 116, 0.03) 4px),
        repeating-linear-gradient(0deg, transparent, transparent 2px, rgba(212, 165, 116, 0.02) 2px, rgba(212, 165, 116, 0.02) 4px);
      pointer-events: none;
      z-index: 1;
    }
    .bg-noise {
      position: fixed;
      inset: 0;
      pointer-events: none;
      opacity: 0.15;
      background-image: radial-gradient(rgba(255, 255, 255, 0.08) 0.5px, transparent 0.5px);
      background-size: 4px 4px;
      z-index: 2;
    }
    .wrap {
      max-width: 1080px;
      margin: 0 auto;
      padding: 24px 20px 40px;
      position: relative;
      z-index: 3;
    }
    .masthead {
      display: flex;
      flex-wrap: wrap;
      align-items: end;
      justify-content: space-between;
      gap: 12px;
      margin-bottom: 16px;
      padding-bottom: 16px;
      border-bottom: 2px solid var(--line);
    }
    h1 {
      margin: 0;
      letter-spacing: 0.09em;
      font-size: clamp(1.8rem, 4vw, 2.75rem);
      font-family: "Fraunces", Georgia, serif;
      line-height: 1.1;
      background: linear-gradient(135deg, var(--accent), var(--accent-warm));
      -webkit-background-clip: text;
      -webkit-text-fill-color: transparent;
      background-clip: text;
    }
    .sub {
      margin: 6px 0 0;
      color: var(--muted);
      font-size: 0.98rem;
      font-style: italic;
    }
    .status-pill {
      margin: 0;
      border: 1px solid var(--accent-dark);
      border-radius: 999px;
      padding: 7px 12px;
      background: rgba(139, 111, 71, 0.3);
      color: var(--accent);
      font-size: 0.92rem;
      font-weight: 600;
    }
    .hero-card, .card, form, .control-bar {
      background: var(--panel);
      border: 1px solid var(--line);
      border-radius: 8px;
      padding: 16px;
      box-shadow: var(--shadow);
      backdrop-filter: blur(8px);
    }
    .hero-card {
      margin-top: 12px;
      position: relative;
      overflow: hidden;
      min-height: 280px;
    }
    .book-scene {
      margin-top: 12px;
      perspective: 1800px;
      perspective-origin: 50% 45%;
    }
    .hero-book {
      margin-top: 0;
      padding: 0;
      min-height: 300px;
      background: transparent;
      border: 0;
      box-shadow: none;
      overflow: visible;
      transform-style: preserve-3d;
    }
    .hero-book::before,
    .hero-book::after {
      display: none;
    }
    .book-spine {
      position: absolute;
      left: 0;
      top: 12px;
      bottom: 12px;
      width: 24px;
      border-radius: 8px 0 0 8px;
      background:
        linear-gradient(180deg, rgba(115, 64, 29, 0.9), rgba(84, 40, 18, 0.95));
      box-shadow: inset -2px 0 8px rgba(0, 0, 0, 0.35);
      z-index: 1;
    }
    .book-cover {
      position: absolute;
      inset: 0;
      border-radius: 10px;
      border: 1px solid #7b5e42;
      background:
        linear-gradient(120deg, rgba(122, 69, 31, 0.97), rgba(82, 40, 21, 0.96)),
        linear-gradient(90deg, transparent 0%, rgba(248, 225, 186, 0.08) 45%, transparent 100%);
      box-shadow: 0 26px 50px rgba(0, 0, 0, 0.4);
      padding: 36px 30px;
      transform-origin: left center;
      transform-style: preserve-3d;
      transition: transform 900ms cubic-bezier(0.25, 0.65, 0.2, 1);
      z-index: 4;
      display: flex;
      flex-direction: column;
      justify-content: center;
      align-items: center;
      text-align: center;
      gap: 8px;
    }
    .cover-kicker {
      margin: 0;
      letter-spacing: 0.2em;
      text-transform: uppercase;
      color: #e8c99f;
      font-size: 0.72rem;
      font-weight: 800;
    }
    .cover-title {
      margin: 0;
      color: #f2dfbf;
      font-family: "Fraunces", Georgia, serif;
      font-size: clamp(1.5rem, 4vw, 2.2rem);
      line-height: 1.15;
    }
    .cover-hint {
      margin: 0;
      color: #ddc19b;
      font-size: 0.9rem;
      font-weight: 600;
    }
    .book-page {
      position: relative;
      border-radius: 10px;
      border: 1px solid #6a5a4f;
      background: linear-gradient(to right, rgba(45, 36, 31, 0.95), rgba(61, 50, 40, 0.95));
      min-height: 300px;
      padding: 32px 40px;
      display: flex;
      flex-direction: column;
      justify-content: center;
      align-items: center;
      text-align: center;
      background-image:
        linear-gradient(90deg, transparent 0%, transparent 45%, rgba(212, 165, 116, 0.06) 47%, rgba(212, 165, 116, 0.06) 53%, transparent 55%, transparent 100%),
        linear-gradient(135deg, rgba(139, 69, 19, 0.15) 0%, transparent 50%, rgba(139, 69, 19, 0.1) 100%);
      box-shadow: 0 24px 44px rgba(0, 0, 0, 0.35);
      z-index: 2;
    }
    .book-page::before {
      content: '';
      position: absolute;
      inset: 0;
      background: radial-gradient(ellipse at center 30%, rgba(248, 245, 240, 0.08), transparent 70%);
      pointer-events: none;
    }
    .book-page::after {
      content: '';
      position: absolute;
      left: 50%;
      top: 0;
      bottom: 0;
      width: 2px;
      background: linear-gradient(to bottom, transparent, rgba(212, 165, 116, 0.4), transparent);
      opacity: 0.8;
    }
    .hero-book.is-open .book-cover {
      transform: rotateY(-145deg) translateZ(0);
    }
    .hero-book.is-closed .book-cover {
      transform: rotateY(0deg) translateZ(0);
    }
    .hero-kicker {
      margin: 0 0 8px;
      text-transform: uppercase;
      letter-spacing: 0.12em;
      font-size: 0.72rem;
      color: var(--accent-dark);
      font-weight: 800;
      position: relative;
      z-index: 1;
    }
    .text {
      font-size: 1.25rem;
      margin: 0 0 12px;
      font-family: "Fraunces", Georgia, serif;
      line-height: 1.6;
      color: var(--page-color);
      position: relative;
      z-index: 1;
      font-weight: 600;
    }
    .hero-card .text {
      font-size: 1.35rem;
    }
    .meta {
      margin: 0 0 10px;
      color: var(--muted);
      display: flex;
      align-items: center;
      gap: 7px;
      flex-wrap: wrap;
      font-size: 0.94rem;
      position: relative;
      z-index: 1;
    }
    .hero-card .meta {
      color: #c8b8a0;
      font-size: 1rem;
    }
    .dot { opacity: 0.6; }
    .badge {
      border: 1px solid var(--accent-dark);
      border-radius: 20px;
      padding: 3px 10px;
      font-size: 0.83rem;
      background: rgba(139, 111, 71, 0.25);
      color: var(--accent);
      font-weight: 600;
    }
    .hero-card .badge {
      background: rgba(212, 165, 116, 0.2);
      border-color: #a68457;
      color: #f0d9c7;
    }
    a {
      color: var(--accent-warm);
      text-decoration: none;
      font-weight: 700;
      transition: color 150ms ease;
    }
    a:hover {
      color: var(--accent);
      text-decoration: underline;
    }
    button {
      width: fit-content;
      background: linear-gradient(135deg, var(--accent-warm), var(--accent));
      color: #1a1410;
      border: 0;
      border-radius: 8px;
      padding: 10px 16px;
      cursor: pointer;
      font-weight: 700;
      transition: transform 120ms ease, box-shadow 120ms ease, background 150ms ease;
      position: relative;
      z-index: 1;
    }
    button:hover {
      transform: translateY(-2px);
      box-shadow: 0 12px 30px rgba(232, 146, 74, 0.25);
      background: linear-gradient(135deg, #f0a855, #dab874);
    }
    button:focus-visible, a:focus-visible, input:focus-visible {
      outline: 3px solid rgba(212, 165, 116, 0.4);
      outline-offset: 2px;
    }
    h2 {
      margin: 22px 0 10px;
      font-family: "Fraunces", Georgia, serif;
      font-size: clamp(1.2rem, 3vw, 1.5rem);
      color: var(--accent-warm);
      border-bottom: 1px solid rgba(212, 165, 116, 0.2);
      padding-bottom: 8px;
    }
    .hero-actions, .card-actions {
      display: flex;
      align-items: center;
      flex-wrap: wrap;
      gap: 10px;
      position: relative;
      z-index: 1;
    }
    .ghost-link {
      border: 1px solid var(--accent-dark);
      border-radius: 8px;
      padding: 8px 14px;
      background: rgba(139, 111, 71, 0.2);
      text-decoration: none;
      color: var(--accent);
      font-weight: 600;
      transition: all 150ms ease;
    }
    .ghost-link:hover {
      background: rgba(139, 111, 71, 0.35);
      border-color: var(--accent);
    }
    .copy-btn {
      background: linear-gradient(135deg, #8b6f47, #7a5d3b);
      font-size: 0.85rem;
      padding: 8px 12px;
    }
    .copy-btn:hover {
      background: linear-gradient(135deg, #a68457, #9b7348);
    }
    .layout-grid {
      display: grid;
      gap: 18px;
      grid-template-columns: 1.25fr 0.95fr;
      align-items: start;
      margin-top: 12px;
    }
    .control-bar {
      display: grid;
      gap: 10px;
      position: sticky;
      top: 12px;
      background: linear-gradient(135deg, rgba(61, 50, 40, 0.9), rgba(45, 36, 31, 0.9));
      border: 1px solid #5a4a3f;
      border-radius: 8px;
      padding: 16px;
    }
    .control-row {
      display: grid;
      gap: 8px;
      grid-template-columns: 1fr 1fr;
    }
    .control-row.single {
      grid-template-columns: 1fr;
    }
    .genre-chips {
      display: flex;
      flex-wrap: wrap;
      gap: 8px;
      margin-top: 2px;
    }
    .genre-chip {
      background: rgba(139, 111, 71, 0.15);
      color: var(--accent);
      border: 1px solid var(--accent-dark);
      border-radius: 20px;
      padding: 6px 12px;
      font-size: 0.82rem;
      font-weight: 600;
      cursor: pointer;
      transition: all 150ms ease;
    }
    .genre-chip:hover {
      background: rgba(139, 111, 71, 0.3);
      border-color: var(--accent-warm);
      color: var(--accent-warm);
    }
    .genre-chip.active {
      background: linear-gradient(135deg, rgba(232, 146, 74, 0.4), rgba(212, 165, 116, 0.3));
      border-color: var(--accent-warm);
      color: var(--page-color);
      font-weight: 700;
      box-shadow: 0 0 12px rgba(232, 146, 74, 0.2);
    }
    form {
      margin: 0;
      display: grid;
      gap: 10px;
      background: linear-gradient(135deg, rgba(61, 50, 40, 0.8), rgba(45, 36, 31, 0.8));
      border: 1px solid #5a4a3f;
    }
    .label {
      font-size: 0.75rem;
      color: var(--accent);
      text-transform: uppercase;
      letter-spacing: 0.08em;
      font-weight: 800;
    }
    input {
      width: 100%;
      padding: 11px 12px;
      border-radius: 6px;
      border: 1px solid #5a4a3f;
      font-size: 0.98rem;
      background: rgba(45, 36, 31, 0.6);
      color: var(--ink);
      transition: all 150ms ease;
    }
    input:focus {
      background: rgba(45, 36, 31, 0.8);
      border-color: var(--accent-dark);
      box-shadow: 0 0 8px rgba(212, 165, 116, 0.2);
    }
    input::placeholder {
      color: rgba(212, 165, 116, 0.4);
    }
    .hint-row {
      display: flex;
      justify-content: space-between;
      align-items: center;
      font-size: 0.78rem;
      color: var(--accent-dark);
    }
    .grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(250px, 1fr));
      gap: 12px;
      align-content: start;
      min-height: 160px;
    }
    .card {
      display: grid;
      gap: 6px;
      opacity: 0;
      transform: translateY(10px);
      animation: rise 360ms ease forwards;
      animation-delay: var(--delay, 0ms);
      background: linear-gradient(135deg, rgba(61, 50, 40, 0.85), rgba(45, 36, 31, 0.85));
      border: 1px solid #6a5a4f;
      border-left: 4px solid var(--accent-dark);
      position: relative;
      overflow: hidden;
    }
    .card::before {
      content: '';
      position: absolute;
      inset: 0;
      background: radial-gradient(ellipse at top right, rgba(232, 146, 74, 0.05), transparent 70%);
      pointer-events: none;
    }
    .card.is-hidden {
      display: none;
    }
    .list-stats {
      margin: 0;
      color: var(--accent-dark);
      font-size: 0.92rem;
      font-weight: 600;
    }
    .empty-state {
      margin: 0;
      border: 1px dashed var(--accent-dark);
      border-radius: 8px;
      padding: 16px;
      color: var(--muted);
      background: rgba(139, 111, 71, 0.1);
      text-align: center;
    }
    .empty-note {
      margin: 0;
      color: var(--muted);
      font-weight: 500;
    }
    .error {
      margin: 12px 0;
      border: 1px solid #c85a5a;
      border-radius: 8px;
      padding: 10px 12px;
      color: #f5a5a5;
      font-weight: 600;
      background: rgba(200, 90, 90, 0.15);
    }
    .to-top {
      position: fixed;
      right: 20px;
      bottom: 18px;
      background: linear-gradient(135deg, var(--accent-warm), var(--accent));
      color: #1a1410;
      z-index: 2;
      opacity: 0;
      pointer-events: none;
      transition: opacity 180ms ease;
      border-radius: 8px;
      border: none;
      padding: 12px 16px;
      font-weight: 700;
    }
    .to-top.show {
      opacity: 1;
      pointer-events: auto;
    }
    .sr-only {
      position: absolute;
      width: 1px;
      height: 1px;
      padding: 0;
      margin: -1px;
      overflow: hidden;
      clip: rect(0, 0, 0, 0);
      white-space: nowrap;
      border: 0;
    }
    @keyframes rise {
      to {
        opacity: 1;
        transform: translateY(0);
      }
    }
    @media (max-width: 980px) {
      .layout-grid {
        grid-template-columns: 1fr;
      }
      .control-bar {
        position: static;
      }
    }
    @media (max-width: 620px) {
      .wrap {
        padding: 18px 14px 30px;
      }
      .control-row {
        grid-template-columns: 1fr;
      }
      .hero-book {
        min-height: 260px;
      }
      .book-cover,
      .book-page {
        min-height: 260px;
        padding: 24px 20px;
      }
      .hero-actions,
      .card-actions {
        align-items: stretch;
      }
      .hero-actions > *,
      .card-actions > * {
        width: 100%;
        text-align: center;
      }
      .to-top {
        right: 14px;
        bottom: 12px;
      }
    }
  </style>
</head>
<body>
  <div class="bg-noise" aria-hidden="true"></div>
  <div class="wrap">
    <header class="masthead">
      <div>
        <h1>QUOTA</h1>
        <p class="sub">Quote + Mood + Music, now with a cleaner and faster reading experience.</p>
      </div>
      <p class="status-pill">{{status}}</p>
    </header>
    {{errorHtml}}

    <h2>Daily Quote</h2>
    {{randomBlock}}

    <section class="layout-grid">
      <aside class="control-bar" aria-label="Quote Controls">
        <h2 style="margin-top:0;">Add New Quote</h2>
        <form method="post" action="/quotes" id="quoteForm" novalidate>
          <label class="label" for="quoteText">Quote Text</label>
          <input id="quoteText" name="text" placeholder="Write a quote worth remembering" maxlength="220" required />
          <div class="hint-row">
            <span>Max 220 characters</span>
            <span id="charCount">0/220</span>
          </div>

          <label class="label" for="quoteAuthor">Author</label>
          <input id="quoteAuthor" name="author" placeholder="Optional" maxlength="80" />

          <button type="submit">Analyze + Save</button>
        </form>

        <h2>Browse Quotes</h2>
        <div class="control-row single">
          <label class="label" for="searchInput">Search</label>
          <input id="searchInput" type="search" placeholder="Search by text or author" aria-label="Search quotes" />
        </div>
        <div class="control-row">
          <div>
            <label class="label" for="sortSelect">Sort</label>
            <input id="sortSelect" list="sortOptions" placeholder="Default" aria-label="Sort quotes" />
            <datalist id="sortOptions">
              <option value="Default"></option>
              <option value="Author (A-Z)"></option>
              <option value="Author (Z-A)"></option>
              <option value="Genre (A-Z)"></option>
            </datalist>
          </div>
          <div>
            <label class="label" for="resultsInfo">Visible</label>
            <input id="resultsInfo" value="{{quotes.Count}}/{{quotes.Count}}" readonly aria-label="Visible quote count" />
          </div>
        </div>

        <div>
          <p class="label" style="margin:0 0 8px;">Genres</p>
          <div class="genre-chips" id="genreChips">
            <button type="button" class="genre-chip active" data-genre-filter="all">All</button>
            {{genreFilters}}
          </div>
        </div>
      </aside>

      <section>
        <h2 style="margin-top:0;">All Quotes</h2>
        <p class="list-stats" id="listStats">{{quotes.Count}} quotes loaded</p>
        <p class="empty-state" id="emptyState" hidden>No quotes match your current filters. Try another search or reset to All.</p>
        <div class="grid" id="quoteGrid">{{items}}</div>
      </section>
    </section>
  </div>

  <button type="button" class="to-top" id="toTop" aria-label="Back to top">Top</button>

  <p class="sr-only" id="liveAnnouncer" aria-live="polite"></p>

  <script>
    (function () {
      const cards = Array.from(document.querySelectorAll('.card'));
      const searchInput = document.getElementById('searchInput');
      const sortSelect = document.getElementById('sortSelect');
      const resultsInfo = document.getElementById('resultsInfo');
      const listStats = document.getElementById('listStats');
      const emptyState = document.getElementById('emptyState');
      const genreChips = Array.from(document.querySelectorAll('.genre-chip'));
      const quoteGrid = document.getElementById('quoteGrid');
      const copyButtons = Array.from(document.querySelectorAll('.copy-btn'));
      const toTop = document.getElementById('toTop');
      const liveAnnouncer = document.getElementById('liveAnnouncer');
      const quoteText = document.getElementById('quoteText');
      const charCount = document.getElementById('charCount');
      const quoteForm = document.getElementById('quoteForm');
      const newRandomBtn = document.getElementById('newRandomBtn');
      const heroBook = document.getElementById('heroBook');
      const heroText = document.getElementById('heroText');
      const heroMeta = document.getElementById('heroMeta');
      const heroMusic = document.getElementById('heroMusic');
      let bookCloseTimer = null;

      function closeBook() {
        if (!heroBook) {
          return;
        }

        heroBook.classList.remove('is-open');
        heroBook.classList.add('is-closed');
      }

      function openBook() {
        if (!heroBook) {
          return;
        }

        heroBook.classList.remove('is-closed');
        heroBook.classList.add('is-open');

        if (bookCloseTimer) {
          clearTimeout(bookCloseTimer);
        }

        const delayMs = Number(heroBook.dataset.closeDelay || '60000');
        bookCloseTimer = setTimeout(() => {
          closeBook();
          liveAnnouncer.textContent = 'Book closed. Click New Random Quote to open it again.';
        }, delayMs);
      }

      let activeGenre = 'all';

      function updateVisibleState() {
        const search = (searchInput ? searchInput.value : '').trim().toLowerCase();
        const sortMode = (sortSelect ? sortSelect.value : 'Default').trim();

        const matching = cards.filter(card => {
          const text = (card.dataset.text || '').toLowerCase();
          const author = (card.dataset.author || '').toLowerCase();
          const genre = (card.dataset.genre || '').toLowerCase();
          const matchesSearch = search.length === 0 || text.includes(search) || author.includes(search);
          const matchesGenre = activeGenre === 'all' || genre === activeGenre;
          const visible = matchesSearch && matchesGenre;
          card.classList.toggle('is-hidden', !visible);
          return visible;
        });

        const sorter = {
          'Author (A-Z)': (a, b) => (a.dataset.author || '').localeCompare(b.dataset.author || ''),
          'Author (Z-A)': (a, b) => (b.dataset.author || '').localeCompare(a.dataset.author || ''),
          'Genre (A-Z)': (a, b) => (a.dataset.genre || '').localeCompare(b.dataset.genre || '')
        };

        if (sorter[sortMode]) {
          matching.sort(sorter[sortMode]);
          matching.forEach(card => quoteGrid.appendChild(card));
        }

        const visibleCount = matching.length;
        const total = cards.length;

        if (resultsInfo) {
          resultsInfo.value = `${visibleCount}/${total}`;
        }
        if (listStats) {
          listStats.textContent = `${visibleCount} of ${total} quotes visible`;
        }
        if (emptyState) {
          emptyState.hidden = visibleCount !== 0;
        }
      }

      if (searchInput) {
        searchInput.addEventListener('input', updateVisibleState);
      }
      if (sortSelect) {
        sortSelect.addEventListener('input', updateVisibleState);
      }

      genreChips.forEach(chip => {
        chip.addEventListener('click', () => {
          activeGenre = (chip.dataset.genreFilter || 'all').toLowerCase();
          genreChips.forEach(other => other.classList.toggle('active', other === chip));
          updateVisibleState();
        });
      });

      copyButtons.forEach(btn => {
        btn.addEventListener('click', async () => {
          const value = btn.dataset.copy || '';
          try {
            await navigator.clipboard.writeText(value);
            liveAnnouncer.textContent = 'Quote copied to clipboard.';
            btn.textContent = 'Copied';
            setTimeout(() => {
              btn.textContent = 'Copy Quote';
            }, 1200);
          } catch {
            liveAnnouncer.textContent = 'Copy failed.';
          }
        });
      });

      if (quoteText && charCount) {
        const updateCount = () => {
          charCount.textContent = `${quoteText.value.length}/220`;
        };
        quoteText.addEventListener('input', updateCount);
        updateCount();
      }

      if (quoteForm && quoteText) {
        quoteForm.addEventListener('submit', (event) => {
          if (!quoteText.value.trim()) {
            event.preventDefault();
            quoteText.focus();
            liveAnnouncer.textContent = 'Quote text is required before submitting.';
          }
        });
      }

      if (toTop) {
        const toggleTop = () => {
          toTop.classList.toggle('show', window.scrollY > 280);
        };
        window.addEventListener('scroll', toggleTop, { passive: true });
        toTop.addEventListener('click', () => window.scrollTo({ top: 0, behavior: 'smooth' }));
        toggleTop();
      }

      if (newRandomBtn && heroText && heroMeta && heroMusic) {
        newRandomBtn.addEventListener('click', async () => {
          newRandomBtn.disabled = true;
          newRandomBtn.textContent = 'Loading...';
          try {
            const response = await fetch('/quote/random', { headers: { 'Accept': 'application/json' } });
            if (!response.ok) {
              throw new Error('Unable to load quote');
            }

            const quote = await response.json();
            heroText.textContent = `"${quote.text}"`;
            heroMeta.innerHTML = `<span>${quote.author}</span><span class="dot">•</span><span class="badge">${quote.genre}</span>`;
            heroMusic.href = quote.musicUrl;
            heroMusic.hidden = false;
            openBook();
            liveAnnouncer.textContent = 'Loaded a new random quote.';
          } catch {
            liveAnnouncer.textContent = 'Failed to load a random quote.';
          } finally {
            newRandomBtn.disabled = false;
            newRandomBtn.textContent = 'New Random Quote';
          }
        });
      }

      closeBook();

      updateVisibleState();
    })();
  </script>
</body>
</html>
""";
}
