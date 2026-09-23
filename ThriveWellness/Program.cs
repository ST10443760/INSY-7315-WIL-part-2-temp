using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using ThriveWellness.Data;
using ThriveWellness.Repositories.Implementations;
using ThriveWellness.Repositories.Interfaces;
using ThriveWellness.Services.Implementations;
using ThriveWellness.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ISessionRepository, SessionRepository>();
builder.Services.AddScoped<ISessionService, SessionService>();
builder.Services.AddScoped<ILocationRepository, LocationRepository>();
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
builder.Services.AddHostedService<ScheduledNotificationHostedService>();

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

// Seed a test admin account for local development.
// TODO: remove this before deploying anywhere near production — the
// username/password below are placeholders for local testing only.
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();

    if (!dbContext.Admins.Any())
    {
        dbContext.Admins.Add(new ThriveWellness.Models.Admin
        {
            Username = "admin",
            PasswordHash = authService.HashPassword("Password123!")
        });
        dbContext.SaveChanges();
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
