using GetSetGo.Web.Data;
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
        return View(await repository.GetMatchingSignalsAsync(User.UserId()));
    }
    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> Set(int id){var result=await trading.SetOrderAsync(User.UserId(),id);TempData[result.Ok?"Success":"Error"]=result.Message;return result.Ok?RedirectToAction("Index","Orders"):RedirectToAction(nameof(Index));}
}
