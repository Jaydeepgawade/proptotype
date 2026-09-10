using GetSetGo.Web.Data;
using GetSetGo.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GetSetGo.Web.Controllers;

[Authorize]
public sealed class DashboardController(IAppRepository repository):Controller
{
    public async Task<IActionResult> Index()
    {
        if(User.IsInRole("Admin"))return RedirectToAction("Index","ResearchAdmin");
        var id=User.UserId(); var risk=await repository.GetRiskProfileAsync(id);
        IReadOnlyList<ResearchSignal> signals=risk is null
            ? Array.Empty<ResearchSignal>()
            : await repository.GetMatchingSignalsAsync(id);
        var orders=await repository.GetOrdersAsync(id);
        return View(new DashboardViewModel{RiskProfile=risk,TotalCapital=await repository.GetAccountCapitalAsync(id),Allocations=await repository.GetRiskProfilesAsync(id),MatchingSignals=signals.Count,SetOrders=orders.Count(x=>x.Status==OrderStatus.Set),OpenTrades=orders.Count(x=>x.Status==OrderStatus.Executed),ActiveRiskAmount=await repository.GetActiveRiskAsync(id)});
    }
}
