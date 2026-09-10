using GetSetGo.Web.Data;
using GetSetGo.Web.Services;
using GetSetGo.Web.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GetSetGo.Web.Controllers.Api;

[ApiController,Authorize(Roles="Client"),Route("api/v1/signals")]
public sealed class SignalsApiController(IAppRepository repository,ITradingService trading):ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        if(await repository.GetRiskProfileAsync(User.UserId()) is null)
            return BadRequest(new{message="Complete Risk Setup before requesting signals."});
        return Ok(await repository.GetMatchingSignalsAsync(User.UserId()));
    }

    [HttpGet("{id:int}/preview")]
    public async Task<IActionResult> Preview(int id)
    {
        var preview = await trading.PreviewSetOrderAsync(User.UserId(), id);
        return preview.Ok ? Ok(preview) : BadRequest(new { message = preview.Message });
    }

    [HttpPost("{id:int}/set"),ValidateAntiForgeryToken]
    public async Task<IActionResult> Set(int id)
    {
        var result=await trading.SetOrderAsync(User.UserId(),id);
        return result.Ok?Ok(new{message=result.Message,orderId=result.OrderId}):BadRequest(new{message=result.Message});
    }
}
