using GetSetGo.Web.Data;
using GetSetGo.Web.Models;
using GetSetGo.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GetSetGo.Web.Controllers;

[Authorize(Roles="Client")]
public sealed class OrdersController(IAppRepository repository, ITradingService trading) : Controller
{
    public IActionResult Index() => RedirectToAction(nameof(Set));
    [HttpGet] public Task<IActionResult> Set() => List("SET", OrderStatus.Set);
    [HttpGet] public Task<IActionResult> Go() => List("GO", OrderStatus.Executed);
    [HttpGet] public Task<IActionResult> Tracker() => List("Tracker", OrderStatus.Executed);
    [HttpGet]
    public async Task<IActionResult> History()
    {
        var orders = await repository.GetOrdersAsync(User.UserId());
        return View("Index", new OrderListViewModel("History",
            orders.Where(o => o.Status != OrderStatus.Set).ToArray()));
    }

    private async Task<IActionResult> List(string section, OrderStatus? status)
    {
        var orders = await repository.GetOrdersAsync(User.UserId());
        return View("Index", new OrderListViewModel(section,
            status is null ? orders : orders.Where(o => o.Status == status).ToArray()));
    }

    [HttpGet]
    public async Task<IActionResult> Trade(int id, string? section = null)
    {
        // This also expires stale SET reservations before rendering execution controls.
        var orders = await repository.GetOrdersAsync(User.UserId());
        var order = orders.FirstOrDefault(o => o.Id == id);
        if (order is null) return NotFound();
        ViewData["ActiveNav"] = section == "History" ? "History" : section == "Tracker" ? "Tracker" : order.Status == OrderStatus.Set ? "Set" : order.Status == OrderStatus.Executed ? "Go" : "Tracker";
        return View(new OrderDetailsViewModel(order, await repository.GetSignalAsync(order.SignalId)));
    }

    [HttpPost, ActionName("Go"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Execute(int id)
    {
        var result = await trading.ExecuteAsync(User.UserId(), id);
        TempData[result.Ok ? "Success" : "Error"] = result.Message;
        return result.Ok ? RedirectToAction(nameof(Go)) : RedirectToAction(nameof(Trade), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var order = await repository.GetOrderAsync(id, User.UserId());
        if (order?.Status == OrderStatus.Set)
        {
            await repository.UpdateOrderStatusAsync(id, User.UserId(), OrderStatus.Cancelled);
            TempData["Success"] = "Order cancelled.";
        }
        return RedirectToAction(nameof(Set));
    }
}
