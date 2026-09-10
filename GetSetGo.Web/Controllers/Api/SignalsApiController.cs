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

    [HttpPost("{id:int}/set"),ValidateAntiForgeryToken]
    public async Task<IActionResult> Set(int id)
    {
        var result=await trading.SetOrderAsync(User.UserId(),id);
        return result.Ok?Ok(new{message=result.Message,orderId=result.OrderId}):BadRequest(new{message=result.Message});
    }
}
