using GetSetGo.Web.Data;
using GetSetGo.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GetSetGo.Web.Controllers;

[Authorize(Roles="Client")]
public sealed class RiskController(IAppRepository repository):Controller
{
    [HttpGet] public async Task<IActionResult> Index()=>View(await repository.GetRiskProfileAsync(User.UserId())??new RiskProfile());
    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(RiskProfile model)
    {
        if(model.MaxTotalRiskPercent<model.RiskPerTradePercent)ModelState.AddModelError(nameof(model.MaxTotalRiskPercent),"Total risk must be at least the per-trade risk.");
        if(!ModelState.IsValid)return View(model);
        model.UserId=User.UserId(); await repository.UpsertRiskProfileAsync(model); TempData["Success"]="Risk profile saved."; return RedirectToAction(nameof(Index));
    }
}
