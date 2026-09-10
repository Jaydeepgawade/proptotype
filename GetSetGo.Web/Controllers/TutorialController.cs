using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GetSetGo.Web.Controllers;

[Authorize(Roles = "Client")]
public sealed class TutorialController : Controller
{
    public IActionResult Index() => View();
}
