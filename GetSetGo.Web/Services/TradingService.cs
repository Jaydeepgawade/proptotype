using GetSetGo.Web.Data;
using GetSetGo.Web.Models;

namespace GetSetGo.Web.Services;

public interface ITradingService
{
    Task<SetOrderPreview> PreviewSetOrderAsync(int userId, int signalId);
    Task<(bool Ok, string Message, int? OrderId)> SetOrderAsync(int userId, int signalId);
    Task<(bool Ok, string Message)> ExecuteAsync(int userId, int orderId);
}

public sealed record SetOrderPreview(bool Ok, string Message, ResearchSignal? Signal, int Quantity, decimal RequiredOrderValue, decimal AvailableStyleCapital, decimal MaximumPossibleLoss);

public sealed class TradingService(IAppRepository repository) : ITradingService
{
    public async Task<(bool Ok,string Message,int? OrderId)> SetOrderAsync(int userId,int signalId)
    {
        var signal=await repository.GetSignalAsync(signalId);
        if(signal is null || !signal.IsActive || DateTime.UtcNow<signal.ValidFromUtc || DateTime.UtcNow>signal.ValidUntilUtc)
            return(false,"This signal is unavailable or expired.",null);
        var risk=await repository.GetRiskProfileAsync(userId, signal.TradingStyle);
        if(risk is null || risk.Capital < 1000) return(false,$"Set a capital allocation for {signal.TradingStyle} first.",null);
        if(signal.RewardRiskRatio<risk.MinimumRewardRiskRatio)
            return(false,"This signal does not match your risk profile.",null);
        if(await repository.HasActiveOrderAsync(userId,signalId)) return(false,"You have already set this signal.",null);

        var perShareRisk=Math.Abs(signal.EntryPrice-signal.StopLoss);
        if(perShareRisk<=0) return(false,"Signal has an invalid stop-loss.",null);
        var allowedRisk=risk.Capital*risk.RiskPerTradePercent/100m;
        var riskBasedQuantity=(int)Math.Floor(allowedRisk/perShareRisk);
        var activeOrders=await repository.GetOrdersAsync(userId);
        var reservedCapital=activeOrders
            .Where(order => order.Status is OrderStatus.Set or OrderStatus.Executed)
            .Sum(order => order.EntryPrice*order.Quantity);
        var styleReservedCapital=activeOrders
            .Where(order => (order.Status is OrderStatus.Set or OrderStatus.Executed) && order.TradingStyle == signal.TradingStyle)
            .Sum(order => order.EntryPrice*order.Quantity);
        var availableStyleCapital=risk.Capital-styleReservedCapital;
        var accountCapital=await repository.GetAccountCapitalAsync(userId);
        var availableCapital=accountCapital-reservedCapital;
        var styleCapitalQuantity=(int)Math.Floor(Math.Max(0, availableStyleCapital)/signal.EntryPrice);
        var accountCapitalQuantity=(int)Math.Floor(Math.Max(0, availableCapital)/signal.EntryPrice);
        var quantity=Math.Min(riskBasedQuantity, Math.Min(styleCapitalQuantity, accountCapitalQuantity));
        if(quantity<1) return(false,"There is not enough available capital for one share.",null);
        var actualRisk=quantity*perShareRisk;
        var activeRisk=activeOrders.Where(order => (order.Status is OrderStatus.Set or OrderStatus.Executed) && order.TradingStyle == signal.TradingStyle).Sum(order => order.RiskAmount);
        var maxRisk=risk.Capital*risk.MaxTotalRiskPercent/100m;
        if(activeRisk+actualRisk>maxRisk) return(false,$"Maximum active-risk limit reached (₹{maxRisk:N2}).",null);

        var id=await repository.AddOrderAsync(new TradeOrder
        {
            UserId=userId,SignalId=signal.Id,TradingStyle=signal.TradingStyle,Symbol=signal.Symbol,Side=signal.Side,
            EntryPrice=signal.EntryPrice,StopLoss=signal.StopLoss,TargetPrice=signal.TargetPrice,
            Quantity=quantity,RiskAmount=actualRisk,ValidUntilUtc=signal.ValidUntilUtc,Status=OrderStatus.Set
        });
        await repository.AddNotificationAsync(userId,$"set:{id}",$"{signal.Symbol} order SET for {quantity} shares.");
        return(true,$"Order set successfully with {quantity} shares.",id);
    }

    public async Task<SetOrderPreview> PreviewSetOrderAsync(int userId, int signalId)
    {
        var signal = await repository.GetSignalAsync(signalId);
        if (signal is null || !signal.IsActive || DateTime.UtcNow < signal.ValidFromUtc || DateTime.UtcNow > signal.ValidUntilUtc)
            return new(false, "This signal is unavailable or expired.", null, 0, 0, 0, 0);
        var risk = await repository.GetRiskProfileAsync(userId, signal.TradingStyle);
        if (risk is null || risk.Capital < 1000) return new(false, $"Set a capital allocation for {signal.TradingStyle} first.", signal, 0, 0, 0, 0);
        if (signal.RewardRiskRatio < risk.MinimumRewardRiskRatio) return new(false, "This signal does not match your risk profile.", signal, 0, 0, 0, 0);
        if (await repository.HasActiveOrderAsync(userId, signalId)) return new(false, "You have already set this signal.", signal, 0, 0, 0, 0);
        var perShareRisk = Math.Abs(signal.EntryPrice - signal.StopLoss);
        if (perShareRisk <= 0) return new(false, "Signal has an invalid stop-loss.", signal, 0, 0, 0, 0);
        var activeOrders = await repository.GetOrdersAsync(userId);
        var styleUsed = activeOrders.Where(order => (order.Status is OrderStatus.Set or OrderStatus.Executed) && order.TradingStyle == signal.TradingStyle).Sum(order => order.EntryPrice * order.Quantity);
        var accountUsed = activeOrders.Where(order => order.Status is OrderStatus.Set or OrderStatus.Executed).Sum(order => order.EntryPrice * order.Quantity);
        var styleAvailable = risk.Capital - styleUsed;
        var accountAvailable = await repository.GetAccountCapitalAsync(userId) - accountUsed;
        var quantity = Math.Min((int)Math.Floor(risk.Capital * risk.RiskPerTradePercent / 100m / perShareRisk), Math.Min((int)Math.Floor(Math.Max(0, styleAvailable) / signal.EntryPrice), (int)Math.Floor(Math.Max(0, accountAvailable) / signal.EntryPrice)));
        if (quantity < 1) return new(false, "There is not enough available capital for one share.", signal, 0, 0, Math.Max(0, styleAvailable), 0);
        var possibleLoss = quantity * perShareRisk;
        var activeRisk = activeOrders.Where(order => (order.Status is OrderStatus.Set or OrderStatus.Executed) && order.TradingStyle == signal.TradingStyle).Sum(order => order.RiskAmount);
        if (activeRisk + possibleLoss > risk.Capital * risk.MaxTotalRiskPercent / 100m) return new(false, "Maximum active-risk limit reached.", signal, 0, 0, styleAvailable, 0);
        return new(true, "Order is ready to SET.", signal, quantity, quantity * signal.EntryPrice, styleAvailable, possibleLoss);
    }

    public async Task<(bool Ok,string Message)> ExecuteAsync(int userId,int orderId)
    {
        var order=await repository.GetOrderAsync(orderId,userId);
        if(order is null) return(false,"Order not found.");
        if(order.Status!=OrderStatus.Set) return(false,"Only a SET order can be executed.");
        if(DateTime.UtcNow>order.ValidUntilUtc)
        {
            await repository.UpdateOrderStatusAsync(orderId,userId,OrderStatus.Expired);
            return(false,"The validity window ended; the order is now expired.");
        }
        await repository.UpdateOrderStatusAsync(orderId,userId,OrderStatus.Executed);
        await repository.AddNotificationAsync(userId,$"executed:{orderId}",$"{order.Symbol} order executed in simulation.");
        return(true,"GO completed—trade executed in simulation mode.");
    }
}
