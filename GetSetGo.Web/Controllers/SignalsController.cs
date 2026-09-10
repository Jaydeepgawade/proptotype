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
        var orders = await repository.GetOrdersAsync(User.UserId());
        var activeStatuses = orders.Where(o => o.Status is OrderStatus.Set or OrderStatus.Executed)
            .GroupBy(o => o.SignalId)
            .ToDictionary(group => group.Key, group => group.Any(o => o.Status == OrderStatus.Executed)
                ? OrderStatus.Executed : OrderStatus.Set);
        return View(new SignalListViewModel(await repository.GetMatchingSignalsAsync(User.UserId()), activeStatuses));
    }
    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> Set(int id){var result=await trading.SetOrderAsync(User.UserId(),id);TempData[result.Ok?"Success":"Error"]=result.Message;return result.Ok?RedirectToAction("Index","Orders"):RedirectToAction(nameof(Index));}
}
