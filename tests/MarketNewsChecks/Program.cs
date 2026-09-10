using System.Net;
using System.Text.Json;
using GetSetGo.Web.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
var now = DateTimeOffset.UtcNow;
string Item(string title, string link, DateTimeOffset date) => $"<item><title>{title}</title><link>{link}</link><pubDate>{date:R}</pubDate><source>Test publisher</source></item>";
var xml = "<rss><channel>" + Item("Company update", "https://news.google.com/rss/articles/one", now) +
    Item("Duplicate", "https://news.google.com/rss/articles/one", now) +
    Item("Old story", "https://news.google.com/rss/articles/old", now.AddDays(-10)) +
    Item("Unsafe link", "javascript:alert(1)", now) +
    Item("Future story", "https://news.google.com/rss/articles/future", now.AddDays(1)) + "</channel></rss>";
var parsed = MarketNewsService.ParseFeed(xml, "Company", now);
Check(parsed.Count == 1 && parsed[0].Title == "Company update", "Feed freshness, deduplication and URL validation");
try { MarketNewsService.ParseFeed("<!DOCTYPE rss [<!ENTITY x SYSTEM 'file:///secret'>]><rss>&x;</rss>", "Company", now); throw new Exception("DTD was allowed"); } catch (System.Xml.XmlException) { }
var valid = "{\"points\":[{\"text\":\"Company update was reported.\",\"sourceIds\":[1]}]}";
Check(MarketNewsService.ParseSummary(valid, [1]).Count == 1, "Valid summary rejected");
foreach (var bad in new[] { "{}", "{\"points\":[{\"text\":\"Fake\",\"sourceIds\":[99]}]}", "{\"points\":[{\"text\":\"No citation\",\"sourceIds\":[]}]}", "{\"points\":[]}" })
{
    try { MarketNewsService.ParseSummary(bad, [1]); throw new Exception("Bad citations accepted"); } catch (JsonException) { }
}
var handler = new StubHandler(xml, valid);
using var client = new HttpClient(handler);
using var cache = new MemoryCache(new MemoryCacheOptions());
var service = new MarketNewsService(new ClientFactory(client), cache, new ConfigurationBuilder().Build(), NullLogger<MarketNewsService>.Instance);
var results = await Task.WhenAll(service.GetAsync("RELIANCE", default), service.GetAsync("RELIANCE", default));
Check(results.All(x => x.Status == "ready" && x.Summary.Count == 1), "AI summary failed");
Check(handler.FeedCalls == 2 && handler.ModelCalls == 1, "Cache did not coalesce concurrent requests");
handler.FailModel = true;
var fallback = await service.GetAsync("TCS", default);
Check(fallback.Status == "news-only" && fallback.Articles.Count > 0 && fallback.Summary.Count == 0, "Model failure must retain news without fake AI text");
handler.EmptyFeeds = true;
var empty = await service.GetAsync("HDFCBANK", default);
Check(empty.Status == "unavailable" && empty.Summary.Count == 0, "No news must not produce AI summary");
Console.WriteLine("PASS: RSS filtering, unsafe XML, citations, concurrent caching, model failure, and empty feeds.");
if (args.Contains("--live"))
{
    using var liveClient = new HttpClient { Timeout = TimeSpan.FromSeconds(120), MaxResponseContentBufferSize = 1_000_000 };
    using var liveCache = new MemoryCache(new MemoryCacheOptions());
    var live = new MarketNewsService(new ClientFactory(liveClient), liveCache, new ConfigurationBuilder().Build(), NullLogger<MarketNewsService>.Instance);
    var result = await live.GetAsync("RELIANCE", default);
    Console.WriteLine($"Live check: status={result.Status}, articles={result.Articles.Count}, summary points={result.Summary.Count}");
    Check(result.Articles.Count > 0, "Live RSS did not return recent headlines");
}
sealed class ClientFactory(HttpClient client) : IHttpClientFactory { public HttpClient CreateClient(string name) => client; }
sealed class StubHandler(string xml, string summary) : HttpMessageHandler
{
    public int FeedCalls, ModelCalls;
    public bool FailModel, EmptyFeeds;
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        if (request.Method == HttpMethod.Get) { FeedCalls++; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(EmptyFeeds ? "<rss><channel/></rss>" : xml) }); }
        ModelCalls++;
        return Task.FromResult(new HttpResponseMessage(FailModel ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new { message = new { content = summary } })) });
    }
}
