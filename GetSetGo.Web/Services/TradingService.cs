using GetSetGo.Web.Data;
using GetSetGo.Web.Models;

namespace GetSetGo.Web.Services;

public interface ITradingService
{
    Task<(bool Ok, string Message, int? OrderId)> SetOrderAsync(int userId, int signalId);
    Task<(bool Ok, string Message)> ExecuteAsync(int userId, int orderId);
}

public sealed class TradingService(IAppRepository repository) : ITradingService
{
    public async Task<(bool Ok,string Message,int? OrderId)> SetOrderAsync(int userId,int signalId)
    {
        var risk=await repository.GetRiskProfileAsync(userId);
        if(risk is null) return(false,"Complete your Risk Setup first.",null);
        var signal=await repository.GetSignalAsync(signalId);
        if(signal is null || !signal.IsActive || DateTime.UtcNow<signal.ValidFromUtc || DateTime.UtcNow>signal.ValidUntilUtc)
            return(false,"This signal is unavailable or expired.",null);
        if(signal.TradingStyle!=risk.TradingStyle || signal.RewardRiskRatio<risk.MinimumRewardRiskRatio)
            return(false,"This signal does not match your risk profile.",null);
        if(await repository.HasActiveOrderAsync(userId,signalId)) return(false,"You have already set this signal.",null);

        var perShareRisk=Math.Abs(signal.EntryPrice-signal.StopLoss);
        if(perShareRisk<=0) return(false,"Signal has an invalid stop-loss.",null);
        var allowedRisk=risk.Capital*risk.RiskPerTradePercent/100m;
        var quantity=(int)Math.Floor(allowedRisk/perShareRisk);
        if(quantity<1) return(false,"Your capital/risk limit is too low for one share.",null);
        var actualRisk=quantity*perShareRisk;
        var activeRisk=await repository.GetActiveRiskAsync(userId);
        var maxRisk=risk.Capital*risk.MaxTotalRiskPercent/100m;
        if(activeRisk+actualRisk>maxRisk) return(false,$"Maximum active-risk limit reached (₹{maxRisk:N2}).",null);

        var id=await repository.AddOrderAsync(new TradeOrder
        {
            UserId=userId,SignalId=signal.Id,Symbol=signal.Symbol,Side=signal.Side,
            EntryPrice=signal.EntryPrice,StopLoss=signal.StopLoss,TargetPrice=signal.TargetPrice,
            Quantity=quantity,RiskAmount=actualRisk,ValidUntilUtc=signal.ValidUntilUtc,Status=OrderStatus.Set
        });
        return(true,$"Order set successfully with {quantity} shares.",id);
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
        return(true,"GO completed—trade executed in simulation mode.");
    }
}
