using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThriveWellness.Authorization;
using ThriveWellness.Models;
using ThriveWellness.Services.Interfaces;

namespace ThriveWellness.Controllers;

// MVC controller: sign-in and sign-out for both kinds of user. The admin
// signs in with a username and password (AuthService); clients sign in with
// Google (ClientAccountService decides which client row that is). Both end
// up with the same cookie, told apart by its Role claim - see AuthPolicies.
public class AccountController : Controller
{
    private readonly IAuthService _authService;
    private readonly IClientAccountService _clientAccountService;
    private readonly IClientDashboardService _dashboardService;
    private readonly IRewardsService _rewardsService;
    private readonly IAuthenticationSchemeProvider _schemeProvider;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        IAuthService authService,
        IClientAccountService clientAccountService,
        IClientDashboardService dashboardService,
        IRewardsService rewardsService,
        IAuthenticationSchemeProvider schemeProvider,
        ILogger<AccountController> logger)
    {
        _authService = authService;
        _clientAccountService = clientAccountService;
        _dashboardService = dashboardService;
        _rewardsService = rewardsService;
        _schemeProvider = schemeProvider;
        _logger = logger;
    }

    // The client sign-in page. returnUrl is only ever honoured when it's a
    // local URL (Url.IsLocalUrl), so a crafted link can't bounce someone to
    // another site after signing in.
    [HttpGet]
    public async Task<IActionResult> ClientLogin(string? returnUrl, string? error)
    {
        var safeReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null;

        if (User.IsClient())
        {
            return LocalRedirectOrDashboard(safeReturnUrl);
        }

        var model = new ClientLoginViewModel
        {
            ReturnUrl = safeReturnUrl,
            GoogleAvailable = await IsGoogleConfiguredAsync(),
            ErrorMessage = error == "google"
                ? "Google sign-in didn't finish. Please try again."
                : TempData["ClientLoginError"] as string
        };

        return View(model);
    }

    // Starts the Google round trip. A POST with an anti-forgery token, so a
    // third-party page can't start a sign-in on someone's behalf. Google
    // returns to /signin-google (handled by the Google middleware), which
    // stores the result in the short-lived External cookie and then sends
    // the browser to GoogleCallback below.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GoogleSignIn(string? returnUrl)
    {
        if (!await IsGoogleConfiguredAsync())
        {
            return RedirectToAction(nameof(ClientLogin));
        }

        var safeReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null;
        var properties = new AuthenticationProperties
        {
            RedirectUri = Url.Action(nameof(GoogleCallback), new { returnUrl = safeReturnUrl })
        };
        return Challenge(properties, GoogleDefaults.AuthenticationScheme);
    }

    // Turns Google's answer into a signed-in client: reads the profile off
    // the External cookie, lets ClientAccountService find, link or create
    // the client row, then swaps the External cookie for the normal sign-in
    // cookie with the client's claims.
    [HttpGet]
    public async Task<IActionResult> GoogleCallback(string? returnUrl)
    {
        var external = await HttpContext.AuthenticateAsync(AuthPolicies.ExternalScheme);
        await HttpContext.SignOutAsync(AuthPolicies.ExternalScheme);

        if (!external.Succeeded || external.Principal == null)
        {
            return RedirectToAction(nameof(ClientLogin), new { error = "google" });
        }

        var googleUser = external.Principal;
        var profile = new GoogleProfile
        {
            Subject = googleUser.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty,
            Email = googleUser.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
            EmailVerified = string.Equals(googleUser.FindFirstValue("email_verified"), "true", StringComparison.OrdinalIgnoreCase),
            Name = googleUser.FindFirstValue(ClaimTypes.Name) ?? string.Empty
        };

        var result = await _clientAccountService.SignInWithGoogleAsync(profile);
        if (!result.Success || result.Client == null)
        {
            TempData["ClientLoginError"] = result.ErrorMessage;
            return RedirectToAction(nameof(ClientLogin), new { returnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null });
        }

        var client = result.Client;
        var identity = new ClaimsIdentity(
            ClientClaims.For(client.ClientId, client.FullName, client.Email),
            CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

        _logger.LogInformation("Client {ClientId} signed in with Google.", client.ClientId);

        return LocalRedirectOrDashboard(Url.IsLocalUrl(returnUrl) ? returnUrl : null);
    }

    // The signed-in client's own page: summary tiles, rewards, upcoming and
    // past bookings, payments and receipts, and their waitlists. Badges are
    // checked first, so a class that has just finished shows up right away.
    [HttpGet]
    [Authorize(Policy = AuthPolicies.ClientOnly)]
    public async Task<IActionResult> Dashboard()
    {
        var clientId = User.GetClientId()!.Value;
        await _rewardsService.EvaluateAsync(clientId);

        var model = await _dashboardService.GetDashboardAsync(clientId, User.FirstName());
        model.Rewards = await _rewardsService.GetRewardsAsync(clientId);
        return View(model);
    }

    // Client sign-out. A POST with an anti-forgery token, so another site
    // can't sign someone out by embedding a link.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ClientSignOut()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
    }

    private async Task<bool> IsGoogleConfiguredAsync() =>
        await _schemeProvider.GetSchemeAsync(GoogleDefaults.AuthenticationScheme) != null;

    private IActionResult LocalRedirectOrDashboard(string? safeReturnUrl) =>
        safeReturnUrl != null ? LocalRedirect(safeReturnUrl) : RedirectToAction(nameof(Dashboard));

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
