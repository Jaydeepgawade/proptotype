using GetSetGo.Web.Data;
using GetSetGo.Web.Models;
using GetSetGo.Web.Services;
using GetSetGo.Web.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GetSetGo.Web.Controllers.Api;

[ApiController,Authorize(Roles="Client"),Route("api/v1/orders")]
public sealed class OrdersApiController(IAppRepository repository,ITradingService trading):ControllerBase
{
    [HttpGet] public async Task<IActionResult> Get()=>Ok(await repository.GetOrdersAsync(User.UserId()));

    [HttpPost("{id:int}/go"),ValidateAntiForgeryToken]
    public async Task<IActionResult> Go(int id)
    {
        var result=await trading.ExecuteAsync(User.UserId(),id);
        return result.Ok?Ok(new{message=result.Message}):BadRequest(new{message=result.Message});
    }

    [HttpPost("{id:int}/cancel"),ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var order=await repository.GetOrderAsync(id,User.UserId());
        if(order is null)return NotFound(new{message="Order not found."});
        if(order.Status!=OrderStatus.Set)return BadRequest(new{message="Only a SET order can be cancelled."});
        await repository.UpdateOrderStatusAsync(id,User.UserId(),OrderStatus.Cancelled);
        return Ok(new{message="Order cancelled."});
    }
}
