using System.Security.Claims;
using ThriveWellness.Authorization;
using ThriveWellness.Services;

namespace ThriveWellness.Tests;

public class HeaderNavTests
{
    private static readonly ClaimsPrincipal Visitor = new(new ClaimsIdentity());

    private static readonly ClaimsPrincipal Client = new(new ClaimsIdentity(
        ClientClaims.For(7, "Thandiwe Nkosi", "thandiwe@example.com"), "Cookies"));

    private static readonly ClaimsPrincipal Admin = new(new ClaimsIdentity(new[]
    {
        new Claim(ClaimTypes.Name, "admin"),
        new Claim(ClaimTypes.Role, AuthPolicies.AdminRole)
    }, "Cookies"));

    [Fact]
    public void Visitor_SeesHomeScheduleAboutAndSignIn()
    {
        var nav = HeaderNav.Build(Visitor, "Home", "Index");

        Assert.Equal(new[] { "Home", "Schedule", "About" }, nav.MainLinks.Select(l => l.Label));
        Assert.NotNull(nav.SignIn);
        Assert.Equal(("Account", "ClientLogin"), (nav.SignIn!.Controller, nav.SignIn.Action));
        Assert.Null(nav.Account);
    }

    [Fact]
    public void NoOne_GetsAnAdminLinkInTheMainBar()
    {
        foreach (var user in new[] { Visitor, Client, Admin })
        {
            Assert.DoesNotContain(HeaderNav.Build(user, "Home", "Index").MainLinks, l => l.Controller == "Admin");
        }
    }

    [Fact]
    public void Client_SeesDashboardAndAProfileMenu()
    {
        var nav = HeaderNav.Build(Client, "Home", "Index");

        Assert.Equal(new[] { "Home", "Schedule", "Dashboard" }, nav.MainLinks.Select(l => l.Label));
        Assert.Null(nav.SignIn);
        var account = nav.Account!;
        Assert.Equal("Thandiwe Nkosi", account.Name);
        Assert.Equal("Thandiwe", account.FirstName);
        Assert.Equal("T", account.Initial);
        Assert.Equal("thandiwe@example.com", account.Subtitle);
        Assert.False(account.IsAdmin);
        Assert.Equal(new[] { "My rewards", "About" }, account.Items.Select(i => i.Label));
        Assert.Equal("ClientSignOut", account.SignOutAction);
    }

    [Fact]
    public void Client_MyRewards_PointsAtTheRewardsPanelOnTheDashboard()
    {
        var rewards = HeaderNav.Build(Client, "Home", "Index").Account!.Items.Single(i => i.Label == "My rewards");

        Assert.Equal(("Account", "Dashboard", "rewards"), (rewards.Controller, rewards.Action, rewards.Fragment));
    }

    [Fact]
    public void Admin_GetsAdminDashboardFirstAndNoClientOnlyLinks()
    {
        var nav = HeaderNav.Build(Admin, "Home", "Index");

        Assert.Equal(new[] { "Home", "Schedule" }, nav.MainLinks.Select(l => l.Label));
        var account = nav.Account!;
        Assert.True(account.IsAdmin);
        Assert.Equal("Admin dashboard", account.Items[0].Label);
        Assert.Equal(("Admin", "Index"), (account.Items[0].Controller, account.Items[0].Action));
        Assert.DoesNotContain(account.Items, i => i.Label == "My rewards");
        Assert.Equal("Logout", account.SignOutAction);
        Assert.Equal("Studio admin", account.Subtitle);
        Assert.Equal("A", account.Initial);
    }

    [Theory]
    [InlineData("Home", "Index", "Home")]
    [InlineData("Booking", "Schedule", "Schedule")]
    [InlineData("Booking", "Book", "Schedule")]
    [InlineData("Account", "Dashboard", "Dashboard")]
    public void ActivePage_IsHighlighted(string controller, string action, string expected)
    {
        var nav = HeaderNav.Build(Client, controller, action);

        Assert.Equal(new[] { expected }, nav.MainLinks.Where(l => l.IsActive).Select(l => l.Label));
    }

    [Fact]
    public void AboutPage_IsHighlightedInTheProfileMenuForAClient()
    {
        var nav = HeaderNav.Build(Client, "Home", "About");

        Assert.DoesNotContain(nav.MainLinks, l => l.IsActive);
        Assert.True(nav.Account!.Items.Single(i => i.Label == "About").IsActive);
    }

    [Fact]
    public void SignInPage_HighlightsTheSignInButton()
    {
        Assert.True(HeaderNav.Build(Visitor, "Account", "ClientLogin").SignIn!.IsActive);
    }

    [Fact]
    public void Client_WithABlankName_GetsAPlaceholderInitial()
    {
        var blank = new ClaimsPrincipal(new ClaimsIdentity(ClientClaims.For(1, "  ", "x@example.com"), "Cookies"));

        Assert.Equal("?", HeaderNav.Build(blank, "Home", "Index").Account!.Initial);
    }

    [Fact]
    public void Client_LowerCaseName_GetsAnUpperCaseInitial()
    {
        var lower = new ClaimsPrincipal(new ClaimsIdentity(ClientClaims.For(1, "zanele khumalo", "z@example.com"), "Cookies"));

        Assert.Equal("Z", HeaderNav.Build(lower, "Home", "Index").Account!.Initial);
    }
}
