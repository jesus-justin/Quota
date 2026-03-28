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

    var randomBlock = random is null
        ? "<p class=\"empty-note\">No quotes yet. Add one to generate your first mood + music pairing.</p>"
        : $"""
          <article class="hero-card" id="heroCard">
            <p class="hero-kicker">Featured Quote</p>
            <p class="text" id="heroText">&ldquo;{System.Net.WebUtility.HtmlEncode(random.Text)}&rdquo;</p>
            <p class="meta" id="heroMeta"><span>{System.Net.WebUtility.HtmlEncode(random.Author)}</span><span class="dot">•</span><span class="badge">{System.Net.WebUtility.HtmlEncode(random.Genre)}</span></p>
            <div class="hero-actions">
              <a id="heroMusic" href="{System.Net.WebUtility.HtmlEncode(random.MusicUrl)}" target="_blank" rel="noopener">Play Mood Music</a>
              <button type="button" id="newRandomBtn">New Random Quote</button>
            </div>
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
  <link rel="preconnect" href="https://fonts.googleapis.com" />
  <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin />
  <link href="https://fonts.googleapis.com/css2?family=Outfit:wght@400;500;600;700;800&family=Fraunces:opsz,wght@9..144,500;9..144,700&display=swap" rel="stylesheet" />
  <style>
    :root {
      --bg: #f4efe6;
      --bg-deep: #efe6d9;
      --panel: rgba(255, 250, 242, 0.8);
      --panel-strong: #fff7eb;
      --ink: #1d130f;
      --accent: #c3572f;
      --accent-2: #0f6f74;
      --muted: #6f5848;
      --line: #e4cdb6;
      --shadow: 0 16px 35px rgba(86, 42, 21, 0.12);
    }
    * { box-sizing: border-box; }
    html { scroll-behavior: smooth; }
    body {
      margin: 0;
      color: var(--ink);
      background:
        radial-gradient(circle at 84% 7%, rgba(245, 182, 126, 0.28) 0, transparent 35%),
        radial-gradient(circle at 13% 90%, rgba(36, 147, 153, 0.2) 0, transparent 30%),
        linear-gradient(160deg, var(--bg) 0%, var(--bg-deep) 100%);
      font-family: "Outfit", "Segoe UI", Tahoma, Geneva, Verdana, sans-serif;
      line-height: 1.55;
    }
    .bg-noise {
      position: fixed;
      inset: 0;
      pointer-events: none;
      opacity: 0.22;
      background-image: radial-gradient(rgba(0, 0, 0, 0.1) 0.4px, transparent 0.4px);
      background-size: 3px 3px;
    }
    .wrap {
      max-width: 1080px;
      margin: 0 auto;
      padding: 24px 20px 40px;
    }
    .masthead {
      display: flex;
      flex-wrap: wrap;
      align-items: end;
      justify-content: space-between;
      gap: 12px;
      margin-bottom: 16px;
    }
    h1 {
      margin: 0;
      letter-spacing: 0.09em;
      font-size: clamp(1.8rem, 4vw, 2.75rem);
      font-family: "Fraunces", Georgia, serif;
      line-height: 1.1;
    }
    .sub {
      margin: 6px 0 0;
      color: var(--muted);
      font-size: 1rem;
    }
    .status-pill {
      margin: 0;
      border: 1px solid var(--line);
      border-radius: 999px;
      padding: 7px 12px;
      background: #fff4e5;
      color: #5a3523;
      font-size: 0.92rem;
      font-weight: 600;
    }
    .hero-card, .card, form, .control-bar {
      background: var(--panel);
      border: 1px solid var(--line);
      border-radius: 16px;
      padding: 16px;
      box-shadow: var(--shadow);
      backdrop-filter: blur(4px);
    }
    .hero-card {
      margin-top: 8px;
      border-left: 8px solid var(--accent);
      background: linear-gradient(135deg, rgba(255, 247, 235, 0.95), rgba(244, 239, 230, 0.95));
    }
    .hero-kicker {
      margin: 0 0 4px;
      text-transform: uppercase;
      letter-spacing: 0.09em;
      font-size: 0.77rem;
      color: #6a4939;
      font-weight: 700;
    }
    .text {
      font-size: 1.1rem;
      margin: 0 0 10px;
      font-family: "Fraunces", Georgia, serif;
    }
    .meta {
      margin: 0 0 10px;
      color: var(--muted);
      display: flex;
      align-items: center;
      gap: 7px;
      flex-wrap: wrap;
      font-size: 0.94rem;
    }
    .dot { opacity: 0.6; }
    .badge {
      border: 1px solid #d8c3ad;
      border-radius: 999px;
      padding: 2px 8px;
      font-size: 0.83rem;
      background: #fff;
    }
    a {
      color: var(--accent-2);
      text-decoration: none;
      font-weight: 700;
    }
    a:hover { text-decoration: underline; }
    button {
      width: fit-content;
      background: var(--accent);
      color: #fff;
      border: 0;
      border-radius: 10px;
      padding: 10px 14px;
      cursor: pointer;
      font-weight: 700;
      transition: transform 120ms ease, box-shadow 120ms ease, background 150ms ease;
    }
    button:hover {
      transform: translateY(-1px);
      box-shadow: 0 8px 20px rgba(86, 42, 21, 0.18);
    }
    button:focus-visible, a:focus-visible, input:focus-visible {
      outline: 3px solid rgba(15, 111, 116, 0.35);
      outline-offset: 2px;
    }
    h2 {
      margin: 22px 0 10px;
      font-family: "Fraunces", Georgia, serif;
      font-size: clamp(1.2rem, 3vw, 1.5rem);
    }
    .hero-actions, .card-actions {
      display: flex;
      align-items: center;
      flex-wrap: wrap;
      gap: 10px;
    }
    .ghost-link {
      border: 1px solid #cae2e3;
      border-radius: 999px;
      padding: 7px 12px;
      background: #f3fbfb;
      text-decoration: none;
    }
    .copy-btn {
      background: #745446;
      font-size: 0.85rem;
      padding: 8px 11px;
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
      background: #fff;
      color: #473328;
      border: 1px solid #d8c3ad;
      border-radius: 999px;
      padding: 6px 10px;
      font-size: 0.82rem;
      font-weight: 600;
    }
    .genre-chip.active,
    .genre-chip:hover {
      background: #fef0df;
      border-color: #ce946d;
      color: #8f401f;
      box-shadow: none;
      transform: none;
    }
    form {
      margin: 0;
      display: grid;
      gap: 10px;
      background: var(--panel-strong);
    }
    .label {
      font-size: 0.82rem;
      color: #654f43;
      text-transform: uppercase;
      letter-spacing: 0.06em;
      font-weight: 700;
    }
    input {
      width: 100%;
      padding: 11px 12px;
      border-radius: 10px;
      border: 1px solid #d8b79a;
      font-size: 0.98rem;
      background: #fff;
      color: var(--ink);
    }
    .hint-row {
      display: flex;
      justify-content: space-between;
      align-items: center;
      font-size: 0.82rem;
      color: #806353;
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
    }
    .card.is-hidden {
      display: none;
    }
    .list-stats {
      margin: 0;
      color: #6f5848;
      font-size: 0.92rem;
      font-weight: 600;
    }
    .empty-state {
      margin: 0;
      border: 1px dashed #cfb59c;
      border-radius: 12px;
      padding: 16px;
      color: #6f5848;
      background: rgba(255, 250, 242, 0.65);
      text-align: center;
    }
    .empty-note {
      margin: 0;
      color: #6f5848;
      font-weight: 500;
    }
    .error {
      margin: 12px 0;
      border: 1px solid #db9f9f;
      border-radius: 12px;
      padding: 10px 12px;
      color: #8f1d1d;
      font-weight: 600;
      background: #fff0f0;
    }
    .to-top {
      position: fixed;
      right: 20px;
      bottom: 18px;
      background: #0f6f74;
      z-index: 2;
      opacity: 0;
      pointer-events: none;
      transition: opacity 180ms ease;
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
      const heroText = document.getElementById('heroText');
      const heroMeta = document.getElementById('heroMeta');
      const heroMusic = document.getElementById('heroMusic');

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
            liveAnnouncer.textContent = 'Loaded a new random quote.';
          } catch {
            liveAnnouncer.textContent = 'Failed to load a random quote.';
          } finally {
            newRandomBtn.disabled = false;
            newRandomBtn.textContent = 'New Random Quote';
          }
        });
      }

      updateVisibleState();
    })();
  </script>
</body>
</html>
""";
}
