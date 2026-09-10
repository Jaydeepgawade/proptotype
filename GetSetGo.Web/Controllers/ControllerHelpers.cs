using System.Security.Claims;

namespace GetSetGo.Web.Controllers;

internal static class ControllerHelpers
{
    public static int UserId(this ClaimsPrincipal user) => int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
