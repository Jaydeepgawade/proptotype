namespace GetSetGo.Web.Models;

public sealed record OrderListViewModel(string Section, IReadOnlyList<TradeOrder> Orders);

public sealed record ClientProfileViewModel(string Name, string Email, decimal TotalCapital, IReadOnlyList<RiskProfile> Profiles, IReadOnlyList<TradeOrder> ActiveOrders);

public sealed record OrderDetailsViewModel(TradeOrder Order, ResearchSignal? Signal, decimal? SimulatedCurrentPrice = null)
{
    public decimal OrderValue => Order.EntryPrice * Order.Quantity;
    public decimal PerShareRisk => Math.Abs(Order.EntryPrice - Order.StopLoss);
    public decimal RewardRiskRatio => PerShareRisk == 0 ? 0 :
        Math.Abs(Order.TargetPrice - Order.EntryPrice) / PerShareRisk;
    public decimal SimulatedPnl => SimulatedCurrentPrice is null ? 0 : (Order.Side == SignalSide.Buy ? SimulatedCurrentPrice.Value - Order.EntryPrice : Order.EntryPrice - SimulatedCurrentPrice.Value) * Order.Quantity;
    public decimal TargetDistance => SimulatedCurrentPrice is null ? 0 : Math.Abs(Order.TargetPrice - SimulatedCurrentPrice.Value);
    public decimal StopLossDistance => SimulatedCurrentPrice is null ? 0 : Math.Abs(SimulatedCurrentPrice.Value - Order.StopLoss);
}
