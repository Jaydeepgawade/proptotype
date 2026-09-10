namespace GetSetGo.Web.Models;

public sealed record SignalListViewModel(
    IReadOnlyList<ResearchSignal> Signals,
    IReadOnlyDictionary<int, OrderStatus> ActiveOrderStatuses,
    IReadOnlyDictionary<int, SignalOrderCheck> OrderChecks);

public sealed record SignalOrderCheck(bool CanSet, string Message, decimal RequiredForOneShare, decimal AvailableStyleCapital);
