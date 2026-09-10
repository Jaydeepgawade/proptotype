using GetSetGo.Web.Data;
using GetSetGo.Web.Models;
using GetSetGo.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GetSetGo.Web.Controllers;

[Authorize(Roles="Client")]
public sealed class SignalsController(IAppRepository repository,ITradingService trading):Controller
{
    public async Task<IActionResult> Index()
    {
        if(await repository.GetRiskProfileAsync(User.UserId()) is null){TempData["Error"]="Complete Risk Setup to receive personalized GET signals.";return RedirectToAction("Index","Risk");}
        var userId = User.UserId();
        var orders = await repository.GetOrdersAsync(userId);
        var signals = await repository.GetMatchingSignalsAsync(userId);
        foreach (var signal in signals)
            await repository.AddNotificationAsync(userId, $"signal:{signal.Id}", $"New matching {signal.TradingStyle} signal: {signal.Symbol} {signal.Side}.");
        var profiles = (await repository.GetRiskProfilesAsync(userId)).ToDictionary(profile => profile.TradingStyle);
        var accountCapital = await repository.GetAccountCapitalAsync(userId);
        var activeOrders = orders.Where(order => order.Status is OrderStatus.Set or OrderStatus.Executed).ToList();
        var accountAvailable = accountCapital - activeOrders.Sum(order => order.EntryPrice * order.Quantity);
        var checks = signals.ToDictionary(signal => signal.Id, signal => BuildOrderCheck(signal, profiles, activeOrders, accountAvailable));
        var activeStatuses = orders.Where(o => o.Status is OrderStatus.Set or OrderStatus.Executed)
            .GroupBy(o => o.SignalId)
            .ToDictionary(group => group.Key, group => group.Any(o => o.Status == OrderStatus.Executed)
                ? OrderStatus.Executed : OrderStatus.Set);
        return View(new SignalListViewModel(signals, activeStatuses, checks));
    }

    private static SignalOrderCheck BuildOrderCheck(ResearchSignal signal, IReadOnlyDictionary<TradingStyle, RiskProfile> profiles, IReadOnlyList<TradeOrder> activeOrders, decimal accountAvailable)
    {
        if (!profiles.TryGetValue(signal.TradingStyle, out var profile) || profile.Capital < 1000)
            return new(false, $"Set a capital allocation for {signal.TradingStyle} first.", signal.EntryPrice, 0);
        var styleUsed = activeOrders.Where(order => order.TradingStyle == signal.TradingStyle).Sum(order => order.EntryPrice * order.Quantity);
        var styleAvailable = Math.Max(0, profile.Capital - styleUsed);
        if (styleAvailable < signal.EntryPrice || accountAvailable < signal.EntryPrice)
        {
            var available = Math.Min(styleAvailable, Math.Max(0, accountAvailable));
            return new(false, $"Need ₹{signal.EntryPrice:N2} for one share; ₹{available:N2} is available.", signal.EntryPrice, available);
        }
        var perShareRisk = Math.Abs(signal.EntryPrice - signal.StopLoss);
        var allowedRisk = profile.Capital * profile.RiskPerTradePercent / 100m;
        if (perShareRisk <= 0 || allowedRisk < perShareRisk)
            return new(false, $"One share can lose ₹{perShareRisk:N2}; your per-trade loss limit is ₹{allowedRisk:N2}.", signal.EntryPrice, styleAvailable);
        var activeRisk = activeOrders.Where(order => order.TradingStyle == signal.TradingStyle).Sum(order => order.RiskAmount);
        var maximumRisk = profile.Capital * profile.MaxTotalRiskPercent / 100m;
        if (activeRisk + perShareRisk > maximumRisk)
            return new(false, $"Maximum active risk is ₹{maximumRisk:N2}; ₹{Math.Max(0, maximumRisk - activeRisk):N2} remains.", signal.EntryPrice, styleAvailable);
        return new(true, $"₹{styleAvailable:N2} available for {signal.TradingStyle}.", signal.EntryPrice, styleAvailable);
    }
    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> Set(int id){var result=await trading.SetOrderAsync(User.UserId(),id);TempData[result.Ok?"Success":"Error"]=result.Message;return result.Ok?RedirectToAction("Index","Orders"):RedirectToAction(nameof(Index));}
}
