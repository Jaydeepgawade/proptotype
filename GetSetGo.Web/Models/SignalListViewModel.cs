namespace GetSetGo.Web.Models;

public sealed record SignalListViewModel(
    IReadOnlyList<ResearchSignal> Signals,
    IReadOnlyDictionary<int, OrderStatus> ActiveOrderStatuses);
