using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using ThriveWellness.Authorization;
using ThriveWellness.Data;
using ThriveWellness.Models;
using ThriveWellness.Repositories.Implementations;
using ThriveWellness.Repositories.Interfaces;
using ThriveWellness.Services;
using ThriveWellness.Services.Implementations;
using ThriveWellness.Services.Interfaces;

// The composition root: the one place that knows about every concrete
// implementation in the app and wires interfaces to them. Everything else
// (controllers, services, repositories) only ever depends on interfaces, so
// this file is also the single place a swapped implementation - a different
// email provider, a mocked repository for a test host - would need to
// change. Split into three parts: builder.Services registrations (what DI
// can construct), the one piece of actual startup work (seeding the admin
// account), and the HTTP pipeline (the order middleware runs in).
var builder = WebApplication.CreateBuilder(args);

// Render (and most container hosts) assign the port to listen on via the
// PORT environment variable at container start, rather than a fixed one.
// Only override the default URL binding when it's actually set, so local
// development (launchSettings.json, or `dotnet run --urls ...`) keeps
// behaving exactly as before - PORT is never set outside a container.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

// Add services to the container.
builder.Services.AddControllersWithViews();

// EF Core's EnableRetryOnFailure is deliberately NOT turned on here: an
// execution strategy that retries can't be combined with the manual
// BeginTransactionAsync calls (WaitlistService, for one) without wrapping
// every one of them in CreateExecutionStrategy().ExecuteAsync - EF throws
// otherwise. Startup resilience is handled by StartupSeeding instead.
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Options pattern: binds the "Payment" section (Payment__AccountHolder/
// Payment__Bank/Payment__AccountNumber as Render env vars) into a typed
// PaymentOptions, injected via IOptions<PaymentOptions> rather than every
// consumer re-reading IConfiguration by string key. Values are optional -
// appsettings.json ships only empty placeholders - so NotificationService
// is the one that decides what "not configured" means for an email.
builder.Services.Configure<PaymentOptions>(builder.Configuration.GetSection("Payment"));

// Every repository and service below is Scoped - one instance per HTTP
// request - matching ApplicationDbContext's own lifetime (it's also
// Scoped, registered just above by AddDbContext). A repository or service
// can't safely be Singleton while it holds a reference to a Scoped
// DbContext, and doesn't need to be Transient since nothing here keeps
// per-call state between methods.
// The real clock, behind .NET's TimeProvider abstraction so AuthService's
// lockout timing can be tested with a fake clock instead of waiting 15
// minutes. Singleton: it holds no state.
builder.Services.AddSingleton(TimeProvider.System);
// Holds failed-login counts for usernames that don't exist (see
// AuthService.Login), so they lock out on the same rule as the real one.
builder.Services.AddMemoryCache();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ISessionRepository, SessionRepository>();
builder.Services.AddScoped<ISessionService, SessionService>();
builder.Services.AddScoped<ILocationRepository, LocationRepository>();
builder.Services.AddScoped<ILocationService, LocationService>();
builder.Services.AddScoped<IClientRepository, ClientRepository>();
builder.Services.AddScoped<IBookingRepository, BookingRepository>();
builder.Services.AddScoped<IBookingService, BookingService>();
builder.Services.AddScoped<IWaitlistRepository, WaitlistRepository>();
builder.Services.AddScoped<IWaitlistService, WaitlistService>();
builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
builder.Services.AddScoped<IPaymentTransactionRepository, PaymentTransactionRepository>();

// The card gateway. SimulatedCardGateway stands in for a real provider -
// moving to PayFast or Stripe means registering their implementation of
// IPaymentGateway here instead; nothing that charges a card changes.
builder.Services.AddScoped<IPaymentGateway, SimulatedCardGateway>();
builder.Services.AddScoped<ICheckoutService, CheckoutService>();
builder.Services.AddScoped<IEmailSender, EmailSender>();

// PaymentService is the Observer-pattern subject (Task 1 doc, Section 9.3).
// The subscription is wired here as a registration step, rather than in
// NotificationService's constructor: NotificationService taking a hard
// dependency on IPaymentService would make this factory circular (it
// needs to resolve INotificationService while still constructing
// IPaymentService). As a Scoped service, NotificationService is never
// actually constructed unless something resolves it - so PaymentController
// (which only knows about IPaymentService) wouldn't otherwise trigger the
// subscription - hence resolving INotificationService here too, within the
// same request scope, without PaymentController ever needing to know
// NotificationService exists.
builder.Services.AddScoped<PaymentService>();
builder.Services.AddScoped<IPaymentService>(sp =>
{
    var paymentService = sp.GetRequiredService<PaymentService>();
    var notificationService = sp.GetRequiredService<INotificationService>();
    paymentService.PaymentConfirmed += notificationService.OnPaymentConfirmed;
    return paymentService;
});
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IScheduledNotificationService, ScheduledNotificationService>();
builder.Services.AddScoped<IAdminDashboardService, AdminDashboardService>();
builder.Services.AddScoped<IAdminSeeder, AdminSeeder>();
builder.Services.AddHostedService<ScheduledNotificationHostedService>();

builder.Services.AddScoped<IClientAccountService, ClientAccountService>();

// Google sign-in for clients is optional configuration: on Render it comes
// from Authentication__Google__ClientId / Authentication__Google__ClientSecret.
// When either is missing the Google scheme simply isn't registered - the
// app still starts, and the client sign-in page hides its button and says
// sign-in isn't available (see AccountController.ClientLogin).
var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
var googleConfigured = !string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleClientSecret);

// Render terminates HTTPS at its proxy and forwards plain HTTP, saying what
// the original scheme was in X-Forwarded-Proto. Trusting those headers is
// what makes Request.Scheme "https" here, so the redirect URI sent to Google
// is https://.../signin-google and matches the one registered with Google.
// Render's proxy addresses aren't fixed, hence clearing the known lists.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// One sign-in cookie for both the admin and clients, told apart by their
// Role claim (see AuthPolicies). HttpOnly stops the cookie being read from
// JavaScript (mitigating session theft via XSS); the 30 minute sliding
// expiry means an active user stays signed in, but an idle tab doesn't stay
// signed in indefinitely.
var authenticationBuilder = builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        // Signed in, but not allowed (for example a client opening an
        // admin page): a plain explanation instead of a bare 403.
        options.AccessDeniedPath = "/Account/AccessDenied";

        // Admin cookies issued before roles existed carry only a Name
        // claim. Only the admin login ever issued cookies back then, so a
        // role-less cookie can only be an admin's: give it the Admin role
        // and re-issue it, so an admin who was already signed in stays
        // signed in. Every cookie issued from now on carries a role.
        options.Events.OnValidatePrincipal = context =>
        {
            if (context.Principal?.Identity is ClaimsIdentity identity
                && identity.IsAuthenticated
                && !identity.HasClaim(c => c.Type == ClaimTypes.Role))
            {
                identity.AddClaim(new Claim(ClaimTypes.Role, AuthPolicies.AdminRole));
                context.ShouldRenew = true;
            }
            return Task.CompletedTask;
        };

        // LoginPath is the admin login. Pages that need a signed-in client
        // (booking, the waitlist, the client dashboard) send people to the
        // client sign-in page instead, carrying a local returnUrl so they
        // land back on the same page afterwards.
        options.Events.OnRedirectToLogin = context =>
        {
            var requiresClient = context.HttpContext.GetEndpoint()?.Metadata
                .GetOrderedMetadata<IAuthorizeData>()
                .Any(a => a.Policy == AuthPolicies.ClientOnly) == true;

            if (requiresClient)
            {
                var request = context.Request;
                var returnUrl = request.PathBase + request.Path + request.QueryString;
                context.RedirectUri = $"{request.PathBase}/Account/ClientLogin?returnUrl={Uri.EscapeDataString(returnUrl)}";
            }

            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
    })
    .AddCookie(AuthPolicies.ExternalScheme, options =>
    {
        options.Cookie.Name = ".ThriveWellness.External";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(10);
    });

if (googleConfigured)
{
    authenticationBuilder.AddGoogle(GoogleDefaults.AuthenticationScheme, options =>
    {
        options.ClientId = googleClientId!;
        options.ClientSecret = googleClientSecret!;
        options.SignInScheme = AuthPolicies.ExternalScheme;
        options.CallbackPath = "/signin-google";
        // Default scopes only (openid, profile, email). email_verified isn't
        // mapped to a claim by default, and ClientAccountService refuses
        // unverified addresses, so map it explicitly.
        options.ClaimActions.MapJsonKey("email_verified", "email_verified");

        // A cancelled consent screen or any other failure coming back from
        // Google returns to the sign-in page with a message instead of an
        // unhandled exception.
        options.Events.OnRemoteFailure = context =>
        {
            context.Response.Redirect("/Account/ClientLogin?error=google");
            context.HandleResponse();
            return Task.CompletedTask;
        };
    });
}

builder.Services.AddAuthorization(AuthPolicies.Configure);

var app = builder.Build();

// Config-driven admin seeding - see AdminSeeder for the rules (missing
// password = no-op, existing account only gets re-hashed if the
// configured password actually changed). Runs in every environment: it's
// a deliberate no-op wherever Admin:Password isn't set, so this is safe
// to leave unconditional rather than gated to Development.
//
// Non-fatal: if the database is unreachable, StartupSeeding logs a warning,
// retries a few times and then lets the app start anyway, instead of the
// process exiting and Render restart-looping the whole site. Each attempt
// gets a fresh scope, so a failed attempt's DbContext isn't reused.
await StartupSeeding.RunWithRetryAsync(async () =>
{
    using var scope = app.Services.CreateScope();
    var adminSeeder = scope.ServiceProvider.GetRequiredService<IAdminSeeder>();
    await adminSeeder.SeedAsync();
}, app.Logger);

// First, so everything after it (HTTPS redirection, authentication and the
// Google redirect URI) sees the original https scheme Render's proxy
// received - see the ForwardedHeadersOptions above.
app.UseForwardedHeaders();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// Middleware order matters - each piece only sees what the one before it
// let through:
// - HTTPS redirect and static files first, before routing even has to run
//   for an asset request.
// - UseRouting has to come before UseAuthentication/UseAuthorization, since
//   those need to know which endpoint was matched (and whether it's the
//   [Authorize]-protected kind) before they can decide anything.
// - UseAuthentication (who is this request from, if anyone - reads the
//   cookie) always runs before UseAuthorization (are they allowed to reach
//   this endpoint) - authorization can't evaluate [Authorize] without
//   authentication having already set the user identity first.
// - Routes are mapped last, since nothing above needs the endpoints to
//   exist yet, only to run before the request actually reaches one.
app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Liveness check for the host: answers as long as the process is up, with
// no database call, so a database outage never makes the app look dead.
// Not linked from anywhere in the UI, and never cached.
app.MapGet("/health", (HttpContext context) =>
{
    context.Response.Headers.CacheControl = "no-store";
    return Results.Text("OK");
}).AllowAnonymous();

// Readiness check for the database: one quick connection test, capped at 3
// seconds so a hanging connection can't hold the request open. The body
// only ever says OK or unavailable - never the host, error or connection
// details.
app.MapGet("/health/db", async (HttpContext context, ApplicationDbContext db) =>
{
    context.Response.Headers.CacheControl = "no-store";

    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
    timeout.CancelAfter(TimeSpan.FromSeconds(3));

    bool canConnect;
    try
    {
        canConnect = await db.Database.CanConnectAsync(timeout.Token);
    }
    catch (Exception)
    {
        // Timeout (OperationCanceledException) or anything else the
        // provider throws - either way the database isn't usable right now.
        canConnect = false;
    }

    return canConnect
        ? Results.Text("OK")
        : Results.Text("Database unavailable", statusCode: StatusCodes.Status503ServiceUnavailable);
}).AllowAnonymous();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
