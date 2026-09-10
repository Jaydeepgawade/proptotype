using System.Security.Claims;
using GetSetGo.Web.Data;
using GetSetGo.Web.Models;
using GetSetGo.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GetSetGo.Web.Controllers.Api;

[ApiController,Route("api/v1/auth")]
public sealed class AuthApiController(IAppRepository repository,IPasswordService passwords):ControllerBase
{
    [AllowAnonymous,HttpPost("login")]
    public async Task<IActionResult> Login(ApiLoginRequest request)
    {
        var user=await repository.FindUserAsync(request.Email);
        if(user is null||!passwords.Verify(request.Password,user.PasswordHash))
            return Unauthorized(new{message="Email or password is incorrect."});
        var claims=new[]{new Claim(ClaimTypes.NameIdentifier,user.Id.ToString()),new Claim(ClaimTypes.Name,user.FullName),new Claim(ClaimTypes.Email,user.Email),new Claim(ClaimTypes.Role,user.Role)};
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,new ClaimsPrincipal(new ClaimsIdentity(claims,CookieAuthenticationDefaults.AuthenticationScheme)));
        return Ok(new{user.Id,user.FullName,user.Email,user.Role});
    }

    [Authorize,HttpPost("logout"),ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout(){await HttpContext.SignOutAsync();return Ok(new{message="Signed out."});}
}
