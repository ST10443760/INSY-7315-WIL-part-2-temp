using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using ThriveWellness.Data;
using ThriveWellness.Repositories.Implementations;
using ThriveWellness.Repositories.Interfaces;
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

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Every repository and service below is Scoped - one instance per HTTP
// request - matching ApplicationDbContext's own lifetime (it's also
// Scoped, registered just above by AddDbContext). A repository or service
// can't safely be Singleton while it holds a reference to a Scoped
// DbContext, and doesn't need to be Transient since nothing here keeps
// per-call state between methods.
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

// Cookie auth is the only scheme - there's one admin account and no API
// clients needing a token-based scheme instead. HttpOnly stops the cookie
// being read from JavaScript (mitigating session theft via XSS); the 30
// minute sliding expiry means an active admin stays signed in, but an idle
// tab doesn't stay signed in indefinitely.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    });

var app = builder.Build();

// Config-driven admin seeding - see AdminSeeder for the rules (missing
// password = no-op, existing account only gets re-hashed if the
// configured password actually changed). Runs in every environment: it's
// a deliberate no-op wherever Admin:Password isn't set, so this is safe
// to leave unconditional rather than gated to Development.
using (var scope = app.Services.CreateScope())
{
    var adminSeeder = scope.ServiceProvider.GetRequiredService<IAdminSeeder>();
    await adminSeeder.SeedAsync();
}

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

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
