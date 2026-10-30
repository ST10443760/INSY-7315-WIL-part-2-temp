using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using ThriveWellness.Authorization;
using ThriveWellness.Controllers;

namespace ThriveWellness.Tests;

// Finds every controller and action by reflection and checks its
// authorization against the real policies, so a new admin page that forgets
// its protection fails here rather than in production.
public class AdminAuthorizationTests
{
    // The only controllers with pages anyone may open. Every other controller
    // is an admin controller and must require AdminOnly as a whole.
    private static readonly HashSet<Type> PublicControllers = new()
    {
        typeof(HomeController),
        typeof(BookingController),
        typeof(AccountController)
    };

    // Admin-only actions that live inside an otherwise public controller.
    private static readonly (Type Controller, string Action)[] AdminActionsInPublicControllers =
    {
        (typeof(BookingController), nameof(BookingController.AdminCancel))
    };

    private static readonly Type[] AllControllers = typeof(HomeController).Assembly
        .GetTypes()
        .Where(t => typeof(Controller).IsAssignableFrom(t) && !t.IsAbstract)
        .ToArray();

    private static IEnumerable<MethodInfo> ActionsOf(Type controller) => controller
        .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
        .Where(m => !m.IsSpecialName && m.GetCustomAttribute<NonActionAttribute>() == null);

    private static IEnumerable<IAuthorizeData> AuthorizeDataFor(Type controller, MethodInfo action) =>
        controller.GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .Concat(action.GetCustomAttributes<AuthorizeAttribute>(inherit: true));

    private static bool IsAnonymousAllowed(Type controller, MethodInfo action) =>
        controller.GetCustomAttribute<AllowAnonymousAttribute>() != null
        || action.GetCustomAttribute<AllowAnonymousAttribute>() != null;

    public static IEnumerable<object[]> AdminActions()
    {
        foreach (var controller in AllControllers.Where(c => !PublicControllers.Contains(c)))
        {
            foreach (var action in ActionsOf(controller))
            {
                yield return new object[] { controller.Name, action.Name };
            }
        }
        foreach (var (controller, action) in AdminActionsInPublicControllers)
        {
            yield return new object[] { controller.Name, action };
        }
    }

    private static (Type Controller, MethodInfo Action) Resolve(string controllerName, string actionName)
    {
        var controller = AllControllers.Single(c => c.Name == controllerName);
        return (controller, ActionsOf(controller).First(m => m.Name == actionName));
    }

    private static async Task<bool> IsAllowedAsync(ClaimsPrincipal user, Type controller, MethodInfo action)
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddAuthorization(AuthPolicies.Configure)
            .BuildServiceProvider();

        var policyProvider = services.GetRequiredService<IAuthorizationPolicyProvider>();
        var policy = await AuthorizationPolicy.CombineAsync(policyProvider, AuthorizeDataFor(controller, action));
        if (policy == null)
        {
            return true;
        }

        var result = await services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(user, policy);
        return result.Succeeded;
    }

    private static ClaimsPrincipal SignedIn(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, "Cookies"));

    private static readonly ClaimsPrincipal Client = SignedIn(
        new Claim(ClaimTypes.Name, "Jane Doe"),
        new Claim(ClaimTypes.Email, "jane@example.com"),
        new Claim("ClientId", "1"),
        new Claim(ClaimTypes.Role, "Client"));

    private static readonly ClaimsPrincipal Admin = SignedIn(
        new Claim(ClaimTypes.Name, "admin"),
        new Claim(ClaimTypes.Role, AuthPolicies.AdminRole));

    private static readonly ClaimsPrincipal Anonymous = new(new ClaimsIdentity());

    [Fact]
    public void Reflection_FindsTheKnownAdminControllers()
    {
        // Guards against the discovery itself silently finding nothing.
        var adminControllers = AllControllers.Where(c => !PublicControllers.Contains(c)).Select(c => c.Name).ToList();

        Assert.Contains(nameof(AdminController), adminControllers);
        Assert.Contains(nameof(PaymentController), adminControllers);
        Assert.Contains(nameof(SessionController), adminControllers);
        Assert.Contains(nameof(LocationController), adminControllers);
        Assert.Contains(nameof(ClientsController), adminControllers);
    }

    [Fact]
    public void EveryAdminController_RequiresAdminOnlyAtClassLevel()
    {
        var unprotected = AllControllers
            .Where(c => !PublicControllers.Contains(c))
            .Where(c => !c.GetCustomAttributes<AuthorizeAttribute>().Any(a => a.Policy == AuthPolicies.AdminOnly))
            .Select(c => c.Name)
            .ToList();

        Assert.Empty(unprotected);
    }

    [Fact]
    public void NoAdminAction_AllowsAnonymousAccess()
    {
        var open = AdminActions()
            .Select(a => Resolve((string)a[0], (string)a[1]))
            .Where(x => IsAnonymousAllowed(x.Controller, x.Action))
            .Select(x => $"{x.Controller.Name}.{x.Action.Name}")
            .ToList();

        Assert.Empty(open);
    }

    [Fact]
    public void NoAuthorizeAttributeAnywhere_IsJustSignedIn()
    {
        // A bare [Authorize] means "any signed-in user", which now includes
        // clients. Every one must name a policy instead.
        var bare = AllControllers
            .SelectMany(c => c.GetCustomAttributes<AuthorizeAttribute>().Select(a => (Where: c.Name, a))
                .Concat(ActionsOf(c).SelectMany(m => m.GetCustomAttributes<AuthorizeAttribute>().Select(a => (Where: $"{c.Name}.{m.Name}", a)))))
            .Where(x => string.IsNullOrEmpty(x.a.Policy) && string.IsNullOrEmpty(x.a.Roles))
            .Select(x => x.Where)
            .ToList();

        Assert.Empty(bare);
    }

    [Theory]
    [MemberData(nameof(AdminActions))]
    public async Task AdminAction_IsRefusedForASignedInClient(string controllerName, string actionName)
    {
        var (controller, action) = Resolve(controllerName, actionName);

        Assert.False(await IsAllowedAsync(Client, controller, action));
    }

    [Theory]
    [MemberData(nameof(AdminActions))]
    public async Task AdminAction_IsRefusedForAnonymousUsers(string controllerName, string actionName)
    {
        var (controller, action) = Resolve(controllerName, actionName);

        Assert.False(await IsAllowedAsync(Anonymous, controller, action));
    }

    [Theory]
    [MemberData(nameof(AdminActions))]
    public async Task AdminAction_IsAllowedForTheAdmin(string controllerName, string actionName)
    {
        var (controller, action) = Resolve(controllerName, actionName);

        Assert.True(await IsAllowedAsync(Admin, controller, action));
    }

    // Booking, the waitlist and the client dashboard need a signed-in
    // client - not the admin, and not an anonymous visitor.
    [Theory]
    [InlineData(nameof(BookingController), nameof(BookingController.Book))]
    [InlineData(nameof(BookingController), nameof(BookingController.Submit))]
    [InlineData(nameof(BookingController), nameof(BookingController.Confirmation))]
    [InlineData(nameof(BookingController), nameof(BookingController.JoinWaitlist))]
    [InlineData(nameof(BookingController), nameof(BookingController.JoinWaitlistSubmit))]
    [InlineData(nameof(AccountController), nameof(AccountController.Dashboard))]
    public async Task ClientAction_IsOnlyAllowedForASignedInClient(string controllerName, string actionName)
    {
        var (controller, action) = Resolve(controllerName, actionName);

        Assert.True(await IsAllowedAsync(Client, controller, action));
        Assert.False(await IsAllowedAsync(Admin, controller, action));
        Assert.False(await IsAllowedAsync(Anonymous, controller, action));
    }

    // The schedule, sign-in pages and the emailed cancellation link must
    // keep working for people who aren't signed in.
    [Theory]
    [InlineData(nameof(BookingController), nameof(BookingController.Schedule))]
    [InlineData(nameof(BookingController), nameof(BookingController.Cancel))]
    [InlineData(nameof(AccountController), nameof(AccountController.ClientLogin))]
    [InlineData(nameof(AccountController), nameof(AccountController.Login))]
    public async Task PublicAction_IsOpenToAnonymousVisitors(string controllerName, string actionName)
    {
        var (controller, action) = Resolve(controllerName, actionName);

        Assert.True(await IsAllowedAsync(Anonymous, controller, action));
    }
}
