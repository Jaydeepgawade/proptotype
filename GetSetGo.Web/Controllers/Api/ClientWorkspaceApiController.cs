using System.Security.Claims;
using GetSetGo.Web.Data;
using GetSetGo.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GetSetGo.Web.Controllers.Api;

[ApiController, Authorize(Roles = "Client"), Route("api/v1/client")]
public sealed class ClientWorkspaceApiController(IAppRepository repository) : ControllerBase
{
    [HttpGet("risk")]
    public async Task<IActionResult> Risk()
    {
        var saved = await repository.GetRiskProfilesAsync(User.UserId());
        return Ok(new RiskSetupViewModel
        {
            TotalCapital = await repository.GetAccountCapitalAsync(User.UserId()),
            Profiles = Enum.GetValues<TradingStyle>().Select(style =>
                saved.FirstOrDefault(p => p.TradingStyle == style) ?? new RiskProfile { TradingStyle = style }).ToList()
        });
    }

    [HttpPut("risk"), ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveRisk(RiskSetupViewModel model)
    {
        var styles = Enum.GetValues<TradingStyle>();
        if (model.Profiles.Count != styles.Length || model.Profiles.Select(p => p.TradingStyle).Distinct().Count() != styles.Length || model.Profiles.Any(p => !Enum.IsDefined(p.TradingStyle)))
            return BadRequest(new { message = "Provide each trading style exactly once." });
        if (model.Profiles.Sum(p => p.Capital) > model.TotalCapital)
            return BadRequest(new { message = "Style allocations cannot exceed total account capital." });
        if (model.Profiles.Any(p => p.Capital > 0 && p.Capital < 1000))
            return BadRequest(new { message = "An enabled style needs at least INR 1,000. Use 0 to disable it." });
        var active = (await repository.GetOrdersAsync(User.UserId())).Where(o => o.Status is OrderStatus.Set or OrderStatus.Executed).ToArray();
        if (active.Sum(o => o.EntryPrice * o.Quantity) > model.TotalCapital)
            return BadRequest(new { message = "Account capital cannot be below the value reserved by active orders." });
        foreach (var profile in model.Profiles)
        {
            var orders = active.Where(o => o.TradingStyle == profile.TradingStyle).ToArray();
            if (orders.Sum(o => o.EntryPrice * o.Quantity) > profile.Capital)
                return BadRequest(new { message = $"{profile.TradingStyle}: allocation cannot be below reserved order value." });
            if (orders.Sum(o => o.RiskAmount) > profile.Capital * profile.MaxTotalRiskPercent / 100m)
                return BadRequest(new { message = $"{profile.TradingStyle}: the limit cannot be below current active risk." });
        }
        await repository.SaveRiskSetupAsync(User.UserId(), model.TotalCapital, model.Profiles);
        return Ok(new { message = "Capital allocations and risk rules saved." });
    }

    [HttpGet("profile")]
    public async Task<IActionResult> Profile()
    {
        var orders = await repository.GetOrdersAsync(User.UserId());
        return Ok(new ClientProfileViewModel(User.Identity?.Name ?? "Client", User.FindFirstValue(ClaimTypes.Email) ?? "",
            await repository.GetAccountCapitalAsync(User.UserId()), await repository.GetRiskProfilesAsync(User.UserId()),
            orders.Where(o => o.Status is OrderStatus.Set or OrderStatus.Executed).ToArray()));
    }

    [HttpGet("notifications")]
    public async Task<IActionResult> Notifications() => Ok(await repository.GetNotificationsAsync(User.UserId()));

    [HttpPost("notifications/read-all"), ValidateAntiForgeryToken]
    public async Task<IActionResult> ReadAll()
    {
        await repository.MarkAllNotificationsReadAsync(User.UserId());
        return Ok(new { message = "All notifications marked as read." });
    }

    [HttpGet("orders/{id:int}")]
    public async Task<IActionResult> Order(int id)
    {
        var order = (await repository.GetOrdersAsync(User.UserId())).FirstOrDefault(o => o.Id == id);
        if (order is null) return NotFound(new { message = "Order not found." });
        var candle = (await repository.GetMarketCandlesAsync(order.Symbol, 1)).LastOrDefault();
        return Ok(new OrderDetailsViewModel(order, await repository.GetSignalAsync(order.SignalId), candle?.Close));
    }
}
