namespace GetSetGo.Web.Models;

public sealed record OrderListViewModel(string Section, IReadOnlyList<TradeOrder> Orders);

public sealed record OrderDetailsViewModel(TradeOrder Order, ResearchSignal? Signal)
{
    public decimal OrderValue => Order.EntryPrice * Order.Quantity;
    public decimal PerShareRisk => Math.Abs(Order.EntryPrice - Order.StopLoss);
    public decimal RewardRiskRatio => PerShareRisk == 0 ? 0 :
        Math.Abs(Order.TargetPrice - Order.EntryPrice) / PerShareRisk;
}
