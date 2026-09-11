using GetSetGo.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GetSetGo.Web.Controllers;
[Authorize(Roles="Admin")]
public sealed class ResearchAudienceController(ResearchAudienceService audience):Controller
{
    [HttpGet]
    public async Task<IActionResult> Preview(int style,decimal entry,decimal stop,decimal target,int side)
    {
        Response.Headers.CacheControl="no-store";
        try{return Json(await audience.Preview(style,entry,stop,target,side));}
        catch(ArgumentException e){return BadRequest(new{message=e.Message});}
    }
    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> Seed()
    {
        await audience.Seed();
        TempData["Success"]="200 demo audience clients are ready. Existing client data was preserved.";
        return RedirectToAction("Create","ResearchAdmin");
    }
}