using GetSetGo.Web.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GetSetGo.Web.Controllers;

[Authorize(Roles = "Client")]
public sealed class TutorialController : Controller
{
    public IActionResult Index() => View();

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetDemoData([FromServices] IAppRepository repository)
    {
        await repository.ResetClientDemoDataAsync(User.UserId());
        TempData["Success"] = "Your prototype data was cleared. Research signals remain available for a fresh demo.";
        return RedirectToAction("Index", "Risk");
    }
}
