using GetSetGo.Web.Data;
using GetSetGo.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GetSetGo.Web.Controllers.Api;

[ApiController,Authorize(Roles="Admin"),Route("api/v1/admin/signals")]
public sealed class ResearchSignalsApiController(IAppRepository repository):ControllerBase
{
    [HttpGet] public async Task<IActionResult> Get()=>Ok(await repository.GetAllSignalsAsync());

    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> Post(ResearchSignalRequest request)
    {
        if(request.ValidUntilUtc<=request.ValidFromUtc)return BadRequest(new{message="Valid-until must be after valid-from."});
        var directionOk=request.Side==SignalSide.Buy
            ? request.StopLoss<request.EntryPrice&&request.TargetPrice>request.EntryPrice
            : request.StopLoss>request.EntryPrice&&request.TargetPrice<request.EntryPrice;
        if(!directionOk)return BadRequest(new{message="BUY requires Stop < Entry < Target; SELL requires Target < Entry < Stop."});
        var signal=new ResearchSignal{Symbol=request.Symbol,Side=request.Side,TradingStyle=request.TradingStyle,EntryPrice=request.EntryPrice,StopLoss=request.StopLoss,TargetPrice=request.TargetPrice,AiNewsSummary=request.AiNewsSummary,ValidFromUtc=request.ValidFromUtc,ValidUntilUtc=request.ValidUntilUtc,IsActive=true};
        if(signal.RewardRiskRatio<1)return BadRequest(new{message="Reward-risk ratio must be at least 1:1."});
        await repository.AddSignalAsync(signal);
        return Created("/api/v1/admin/signals",new{message="Research signal published.",signal});
    }
}
