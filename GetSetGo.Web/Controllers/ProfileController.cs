using GetSetGo.Web.Data;
using GetSetGo.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GetSetGo.Web.Controllers;

[Authorize(Roles = "Client")]
public sealed class ProfileController(IAppRepository repository) : Controller
{
    public async Task<IActionResult> Index()
    {
        var userId = User.UserId();
        var orders = await repository.GetOrdersAsync(userId);
        return View(new ClientProfileViewModel(User.Identity?.Name ?? "Client", User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "", await repository.GetAccountCapitalAsync(userId), await repository.GetRiskProfilesAsync(userId), orders.Where(order => order.Status is OrderStatus.Set or OrderStatus.Executed).ToArray()));
    }
}
