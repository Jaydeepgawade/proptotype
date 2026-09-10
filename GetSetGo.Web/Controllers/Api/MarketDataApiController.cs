using GetSetGo.Web.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GetSetGo.Web.Controllers.Api;

[ApiController,Authorize(Roles="Client"),Route("api/v1/market-data")]
public sealed class MarketDataApiController(IAppRepository repository):ControllerBase
{
    [HttpGet("{symbol}")]
    public async Task<IActionResult> Get(string symbol,[FromQuery]string timeframe="1D",[FromQuery]int take=60)
    {
        if(!string.Equals(timeframe,"1D",StringComparison.OrdinalIgnoreCase))
            return BadRequest(new{message="The demo data currently supports only the 1D timeframe."});
        if(string.IsNullOrWhiteSpace(symbol)||symbol.Length>30)
            return BadRequest(new{message="A valid symbol is required."});
        var candles=await repository.GetMarketCandlesAsync(symbol,take);
        return candles.Count==0
            ? NotFound(new{message="No demo OHLC data exists for this symbol."})
            : Ok(new{symbol=symbol.ToUpperInvariant(),timeframe="1D",isDemo=true,candles});
    }
}
