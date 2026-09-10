using GetSetGo.Web.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GetSetGo.Web.Controllers;

[Authorize(Roles = "Client")]
public sealed class NotificationsController(IAppRepository repository) : Controller
{
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllRead()
    {
        await repository.MarkAllNotificationsReadAsync(User.UserId());
        return Redirect(Request.Headers.Referer.ToString());
    }
}
