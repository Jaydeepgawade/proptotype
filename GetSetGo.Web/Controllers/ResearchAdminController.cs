using GetSetGo.Web.Data;
using GetSetGo.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GetSetGo.Web.Controllers;

[Authorize(Roles="Admin")]
public sealed class ResearchAdminController(IAppRepository repository):Controller
{
    public async Task<IActionResult> Index()=>View(await repository.GetAllSignalsAsync());
    [HttpGet] public IActionResult Create()=>View(new ResearchSignal{ValidFromUtc=DateTime.UtcNow,ValidUntilUtc=DateTime.UtcNow.AddDays(2)});
    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ResearchSignal model)
    {
        if(model.ValidUntilUtc<=model.ValidFromUtc)ModelState.AddModelError(nameof(model.ValidUntilUtc),"Valid-until must be after valid-from.");
        var directionOk=model.Side==SignalSide.Buy?model.StopLoss<model.EntryPrice&&model.TargetPrice>model.EntryPrice:model.StopLoss>model.EntryPrice&&model.TargetPrice<model.EntryPrice;
        if(!directionOk)ModelState.AddModelError("","BUY requires Stop < Entry < Target; SELL requires Target < Entry < Stop.");
        if(model.RewardRiskRatio<1)ModelState.AddModelError("","Reward-risk ratio must be at least 1:1.");
        if(!ModelState.IsValid)return View(model);
        await repository.AddSignalAsync(model);TempData["Success"]="Research signal published.";return RedirectToAction(nameof(Index));
    }
}
