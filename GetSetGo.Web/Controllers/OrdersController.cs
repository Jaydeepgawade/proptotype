using GetSetGo.Web.Data;
using GetSetGo.Web.Models;
using GetSetGo.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GetSetGo.Web.Controllers;

[Authorize(Roles="Client")]
public sealed class OrdersController(IAppRepository repository,ITradingService trading):Controller
{
    public async Task<IActionResult> Index()=>View(await repository.GetOrdersAsync(User.UserId()));
    [HttpPost,ValidateAntiForgeryToken] public async Task<IActionResult> Go(int id){var r=await trading.ExecuteAsync(User.UserId(),id);TempData[r.Ok?"Success":"Error"]=r.Message;return RedirectToAction(nameof(Index));}
    [HttpPost,ValidateAntiForgeryToken] public async Task<IActionResult> Cancel(int id){var order=await repository.GetOrderAsync(id,User.UserId());if(order?.Status==OrderStatus.Set){await repository.UpdateOrderStatusAsync(id,User.UserId(),OrderStatus.Cancelled);TempData["Success"]="Order cancelled.";}return RedirectToAction(nameof(Index));}
}
