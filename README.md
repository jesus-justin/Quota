# Quota

Quota is a C#-driven quote app that pairs each quote with a mood and music link.
It uses Gemini API analysis when configured and falls back to a default mood when the API key is missing.

## Run

1. Open a terminal in `QUOTA/`.
2. Run `dotnet run`.
3. Open `http://localhost:5001`.

## Notes

- Keep the terminal running while using the site.
- If port 5001 is unavailable, set `QUOTA_URL` before running, for example:
	- PowerShell: `$env:QUOTA_URL = "http://localhost:5010"`
