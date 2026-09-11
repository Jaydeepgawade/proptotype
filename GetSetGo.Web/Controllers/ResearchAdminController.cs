using GetSetGo.Web.Data;
using GetSetGo.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GetSetGo.Web.Controllers;

[Authorize(Roles="Admin")]
public sealed class ResearchAdminController(IAppRepository repository):Controller
{
    public async Task<IActionResult> Index()=>View(await repository.GetAllSignalsAsync());
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await LoadPublishedSymbolsAsync();
        var now = DateTime.UtcNow;
        now = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, DateTimeKind.Utc);
        return View(new ResearchSignal{ValidFromUtc=now,ValidUntilUtc=now.AddDays(2)});
    }
    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ResearchSignal model)
    {
        ValidateSignal(model);
        if(!ModelState.IsValid)
        {
            await LoadPublishedSymbolsAsync();
            return View(model);
        }
        if (!model.AiNewsSummary.StartsWith("[AI RESEARCH]", StringComparison.Ordinal))
            model.AiNewsSummary = $"[AI RESEARCH] Generated research brief\n\n{model.AiNewsSummary.Trim()}";
        await repository.AddSignalAsync(model);TempData["Success"]="Research signal published.";return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var signal = await repository.GetSignalAsync(id);
        if (signal is null) { TempData["Error"] = "Research signal was not found."; return RedirectToAction(nameof(Index)); }
        return View(signal);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(ResearchSignal model)
    {
        ValidateSignal(model);
        if (!ModelState.IsValid) return View(model);
        if (!await repository.UpdateSignalAsync(model)) { TempData["Error"] = "Research signal was not found."; return RedirectToAction(nameof(Index)); }
        TempData["Success"] = "Research signal updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(int id)
    {
        var changed = await repository.DeactivateSignalAsync(id);
        TempData[changed ? "Success" : "Error"] = changed ? "Research signal deactivated. Existing orders are unchanged." : "Only an active research signal can be deactivated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Expire(int id)
    {
        var changed = await repository.ExpireSignalAsync(id);
        TempData[changed ? "Success" : "Error"] = changed ? "Research signal expired. Existing orders are unchanged." : "Only an active, unexpired research signal can be expired.";
        return RedirectToAction(nameof(Index));
    }

    private void ValidateSignal(ResearchSignal model)
    {
        if(model.ValidUntilUtc<=model.ValidFromUtc)ModelState.AddModelError(nameof(model.ValidUntilUtc),"Valid-until must be after valid-from.");
        if(model.ValidUntilUtc<=DateTime.UtcNow)ModelState.AddModelError(nameof(model.ValidUntilUtc),"Valid-until must be in the future.");
        var directionOk=model.Side==SignalSide.Buy?model.StopLoss<model.EntryPrice&&model.TargetPrice>model.EntryPrice:model.StopLoss>model.EntryPrice&&model.TargetPrice<model.EntryPrice;
        if(!directionOk)ModelState.AddModelError("","BUY requires Stop < Entry < Target; SELL requires Target < Entry < Stop.");
        if(model.RewardRiskRatio<1)ModelState.AddModelError("","Reward-risk ratio must be at least 1:1.");
    }

    private async Task LoadPublishedSymbolsAsync()
    {
        ViewData["PublishedSymbols"] = (await repository.GetAllSignalsAsync())
            .Where(signal => signal.IsActive)
            .Select(signal => signal.Symbol.ToUpperInvariant())
            .Distinct()
            .ToArray();
    }
}


