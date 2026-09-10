using GetSetGo.Web.Data;
using GetSetGo.Web.Models;
using GetSetGo.Web.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GetSetGo.Web.Controllers.Api;

[ApiController,Authorize(Roles="Client"),Route("api/v1/risk-profile")]
public sealed class RiskProfilesApiController(IAppRepository repository):ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var profile=await repository.GetRiskProfileAsync(User.UserId());
        return profile is null?NotFound(new{message="Risk profile is not configured."}):Ok(profile);
    }

    [HttpPut,ValidateAntiForgeryToken]
    public async Task<IActionResult> Put(RiskProfileRequest request)
    {
        if(request.MaxTotalRiskPercent<request.RiskPerTradePercent)
            return BadRequest(new{message="Maximum total risk must be at least the per-trade risk."});
        var profile=new RiskProfile{UserId=User.UserId(),Capital=request.Capital,TradingStyle=request.TradingStyle,RiskPerTradePercent=request.RiskPerTradePercent,MaxTotalRiskPercent=request.MaxTotalRiskPercent,MinimumRewardRiskRatio=request.MinimumRewardRiskRatio,IsActive=true};
        await repository.UpsertRiskProfileAsync(profile);
        return Ok(new{message="Risk profile saved.",profile});
    }
}
