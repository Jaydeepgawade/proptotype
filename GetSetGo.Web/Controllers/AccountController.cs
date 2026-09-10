using System.Security.Claims;
using GetSetGo.Web.Data;
using GetSetGo.Web.Models;
using GetSetGo.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GetSetGo.Web.Controllers;

public sealed class AccountController(IAppRepository repository,IPasswordService passwords):Controller
{
    [AllowAnonymous, HttpGet]
    public IActionResult Login(string? returnUrl=null)=>View(new LoginViewModel{ReturnUrl=returnUrl});

    [AllowAnonymous, HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if(!ModelState.IsValid)return View(model);
        var user=await repository.FindUserAsync(model.Email);
        if(user is null || !passwords.Verify(model.Password,user.PasswordHash))
        {
            ModelState.AddModelError("","Email or password is incorrect."); return View(model);
        }
        var claims=new[]{new Claim(ClaimTypes.NameIdentifier,user.Id.ToString()),new Claim(ClaimTypes.Name,user.FullName),new Claim(ClaimTypes.Email,user.Email),new Claim(ClaimTypes.Role,user.Role)};
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,new ClaimsPrincipal(new ClaimsIdentity(claims,CookieAuthenticationDefaults.AuthenticationScheme)));
        if(!string.IsNullOrWhiteSpace(model.ReturnUrl)&&Url.IsLocalUrl(model.ReturnUrl))return LocalRedirect(model.ReturnUrl);
        return RedirectToAction("Index","Dashboard");
    }

    [Authorize,HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout(){await HttpContext.SignOutAsync();return RedirectToAction(nameof(Login));}
    [AllowAnonymous] public IActionResult AccessDenied()=>View();
}
