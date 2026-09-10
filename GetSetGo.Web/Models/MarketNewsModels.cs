namespace GetSetGo.Web.Models;

public sealed record NewsArticle(int Id, string Title, string Url, string Publisher, DateTimeOffset PublishedUtc, string Category);
public sealed record NewsPoint(string Text, int[] SourceIds);
public sealed record MarketNewsResult(string Symbol, string Company, string Status, string Message,
    DateTimeOffset FetchedUtc, DateTimeOffset NextRefreshUtc, IReadOnlyList<NewsArticle> Articles,
    IReadOnlyList<NewsPoint> Summary);
