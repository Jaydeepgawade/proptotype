# Market news and local AI summaries

The trading workspace loads `GET /api/v1/orders/{id}/news` separately from the chart. It requires a Client login and verifies ownership of the order before retrieving news.

## How it works

- Google News search RSS supplies recent headline metadata and publisher links. This is an external aggregator feed, not an exchange feed or a guaranteed news API. Its availability and coverage can change.
- Supported company mappings: RELIANCE, TCS, HDFCBANK and M&M. Add other verified full company names under `MarketNews:Companies:SYMBOL` in configuration.
- Company searches cover seven days and the market search requests two days; the parser rejects anything older than seven days, future timestamps, invalid links and duplicates.
- Only headlines, publisher names and timestamps are sent to the local model. Full articles are not scraped. A headline summary cannot replace reading the linked reporting.
- Ollama generates up to four points with source IDs. Invalid output or invented source IDs cause a news-only fallback. Citation validation does not guarantee that every generated claim is correct; users should check the sources.
- Valid summaries are cached for 10 minutes per symbol. News-only and unavailable results retry after 30 seconds. Refresh uses this cache and shows the next refresh time. Model generation is serialized to limit load on a local PC.
- No model response changes prices, risk settings, order state or execution. The old seeded note remains under Original research note.

## Local setup (Windows)

Install Ollama from https://ollama.com/download/windows or:

```powershell
winget install --id Ollama.Ollama --exact
ollama pull llama3.2:3b
```

The model is a multi-gigabyte download and uses your PC's memory/CPU/GPU. It needs no paid API key. Ollama must remain running while the app generates summaries. If it is not running, launch the Ollama application or use `ollama serve`.

Defaults (optional overrides in Manage User Secrets or environment variables):

```json
{
  "MarketNews": {
    "OllamaModel": "llama3.2:3b",
    "OllamaUrl": "http://localhost:11434/api/chat",
    "Companies": {
      "INFY": "Infosys"
    }
  }
}
```

The endpoint is deliberately limited to loopback addresses. A Docker-hosted web app requires a separate deployment configuration; the Windows host's localhost is not the container's localhost. This setup targets the existing local Visual Studio profile.

Restart the web app, open SET, choose an order, and check Before you execute. The UI explicitly distinguishes AI summaries, news-only fallback, no recent news, unsupported symbols and expired login sessions. Summaries currently use English to match the application.

## Verification

```powershell
dotnet run --project tests/MarketNewsChecks
# Also retrieves real news and tries the local model without connecting to SQL:
dotnet run --project tests/MarketNewsChecks -- --live
node --check GetSetGo.Web/wwwroot/js/market-news.js
```

Tests cover RSS freshness, duplicates, unsafe URLs/XML, summary references, caching of concurrent requests, model outage and empty feeds. The live check reports whether an AI summary was actually generated.

## External documentation

- Ollama structured output: https://docs.ollama.com/capabilities/structured-outputs
- Windows setup: https://docs.ollama.com/windows

Review news-source licensing and attribution requirements before publishing or commercializing the prototype. A public RSS endpoint is not a license to republish full articles.
