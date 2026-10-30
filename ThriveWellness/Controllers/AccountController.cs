using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using ThriveWellness.Authorization;
using ThriveWellness.Models;
using ThriveWellness.Services.Interfaces;

namespace ThriveWellness.Controllers;

// MVC controller: the admin login/logout pages. Delegates the actual
// username/password check to AuthService and otherwise just deals with
// ASP.NET Core's cookie authentication - issuing the sign-in cookie on
// success, clearing it on logout.
public class AccountController : Controller
{
    private readonly IAuthService _authService;

    public AccountController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpGet]
    public IActionResult Login()
    {
        return View();
    }

    // Verifies the submitted credentials through AuthService and, on
    // success, signs the admin in with a cookie carrying their username and
    // the Admin role - the role is what the AdminOnly policy on every admin
    // controller checks for. On failure, shows the same view again with a
    // single generic error (not "wrong password" vs "no such user" - see
    // AuthService.Login for why that distinction is never surfaced).
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _authService.Login(model.Username, model.Password);
        if (result == LoginResult.LockedOut)
        {
            // Same wording whether or not the username exists - unknown
            // usernames lock out on the same rule (see AuthService.Login).
            ModelState.AddModelError(string.Empty, "Too many attempts, try again later.");
            return View(model);
        }
        if (result != LoginResult.Success)
        {
            ModelState.AddModelError(string.Empty, "Invalid username or password.");
            return View(model);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, model.Username),
            new(ClaimTypes.Role, AuthPolicies.AdminRole)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

        return RedirectToAction("Index", "Admin");
    }

    // Where the cookie scheme sends someone who is signed in but not allowed
    // in - a client opening an admin page, for example. Deliberately says
    // nothing about what the page was.
    [HttpGet]
    public IActionResult AccessDenied()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    // Clears the sign-in cookie - this is the entire logout flow, since
    // there's no server-side session state to tear down.
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Login");
    }
}
