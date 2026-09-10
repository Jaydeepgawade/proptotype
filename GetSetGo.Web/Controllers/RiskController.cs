using GetSetGo.Web.Data;
using GetSetGo.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GetSetGo.Web.Controllers;

[Authorize(Roles="Client")]
public sealed class RiskController(IAppRepository repository) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var userId = User.UserId();
        var saved = await repository.GetRiskProfilesAsync(userId);
        var legacy = saved.FirstOrDefault();
        var profiles = Enum.GetValues<TradingStyle>().Select(style =>
            saved.FirstOrDefault(profile => profile.TradingStyle == style)
            ?? new RiskProfile { TradingStyle = style, RiskPerTradePercent = 1, MaxTotalRiskPercent = 3, MinimumRewardRiskRatio = 1 }).ToList();
        var totalCapital = await repository.GetAccountCapitalAsync(userId);
        return View(new RiskSetupViewModel { TotalCapital = totalCapital > 0 ? totalCapital : legacy?.Capital ?? 100000, Profiles = profiles });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(RiskSetupViewModel model)
    {
        if (model.Profiles.Count != Enum.GetValues<TradingStyle>().Length)
            ModelState.AddModelError("", "Configure each trading style.");
        if (model.Profiles.Sum(profile => profile.Capital) > model.TotalCapital)
            ModelState.AddModelError(nameof(model.TotalCapital), "Style allocations cannot exceed total trading capital.");
        foreach (var profile in model.Profiles)
        {
            if (profile.Capital < 0) ModelState.AddModelError("", "A style allocation cannot be negative.");
            if (profile.Capital > 0 && profile.Capital < 1000) ModelState.AddModelError("", "An enabled style needs at least ₹1,000 capital.");
            if (profile.MaxTotalRiskPercent < profile.RiskPerTradePercent)
                ModelState.AddModelError("", $"{profile.TradingStyle}: total risk must be at least the per-trade risk.");
        }
        if (!ModelState.IsValid) return View(model);
        await repository.SaveRiskSetupAsync(User.UserId(), model.TotalCapital, model.Profiles);
        TempData["Success"] = "Capital allocations and risk rules saved.";
        return RedirectToAction(nameof(Index));
    }
}
