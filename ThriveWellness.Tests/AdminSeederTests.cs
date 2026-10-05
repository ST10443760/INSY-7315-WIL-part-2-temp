using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ThriveWellness.Data;
using ThriveWellness.Models;
using ThriveWellness.Services.Implementations;
using ThriveWellness.Services.Interfaces;

namespace ThriveWellness.Tests;

public class AdminSeederTests
{
    private readonly Mock<IAuthService> _authService = new();

    private static ApplicationDbContext NewInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static IConfiguration ConfigWith(string? username, string? password)
    {
        var values = new Dictionary<string, string?>();
        if (username != null)
        {
            values["Admin:Username"] = username;
        }
        if (password != null)
        {
            values["Admin:Password"] = password;
        }
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private AdminSeeder CreateSeeder(ApplicationDbContext context, string? username, string? password)
    {
        return new AdminSeeder(context, _authService.Object, ConfigWith(username, password), NullLogger<AdminSeeder>.Instance);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task SeedAsync_NoPasswordConfigured_CreatesNothing(string? password)
    {
        using var context = NewInMemoryContext();
        var seeder = CreateSeeder(context, "admin", password);

        await seeder.SeedAsync();

        Assert.Equal(0, await context.Admins.CountAsync());
        _authService.Verify(a => a.HashPassword(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SeedAsync_NoExistingAdmin_CreatesOneWithAHashedPassword()
    {
        using var context = NewInMemoryContext();
        _authService.Setup(a => a.HashPassword("correct-horse-battery-staple")).Returns("hashed-value");
        var seeder = CreateSeeder(context, "admin", "correct-horse-battery-staple");

        await seeder.SeedAsync();

        var created = await context.Admins.SingleAsync();
        Assert.Equal("admin", created.Username);
        Assert.Equal("hashed-value", created.PasswordHash);
    }

    [Fact]
    public async Task SeedAsync_NoUsernameConfigured_DefaultsToAdmin()
    {
        using var context = NewInMemoryContext();
        _authService.Setup(a => a.HashPassword(It.IsAny<string>())).Returns("hashed-value");
        var seeder = CreateSeeder(context, username: null, password: "some-password");

        await seeder.SeedAsync();

        var created = await context.Admins.SingleAsync();
        Assert.Equal("admin", created.Username);
    }

    [Fact]
    public async Task SeedAsync_ExistingAdminWithChangedPassword_ReplacesTheHash()
    {
        using var context = NewInMemoryContext();
        var realHash = BCrypt.Net.BCrypt.HashPassword("old-password");
        context.Admins.Add(new Admin { Username = "admin", PasswordHash = realHash });
        await context.SaveChangesAsync();

        _authService.Setup(a => a.HashPassword("new-password")).Returns("new-hashed-value");
        var seeder = CreateSeeder(context, "admin", "new-password");

        await seeder.SeedAsync();

        var stored = await context.Admins.SingleAsync();
        Assert.Equal("new-hashed-value", stored.PasswordHash);
        _authService.Verify(a => a.HashPassword("new-password"), Times.Once);
    }

    [Fact]
    public async Task SeedAsync_ExistingAdminWithUnchangedPassword_LeavesTheHashAlone()
    {
        using var context = NewInMemoryContext();
        var realHash = BCrypt.Net.BCrypt.HashPassword("same-password");
        context.Admins.Add(new Admin { Username = "admin", PasswordHash = realHash });
        await context.SaveChangesAsync();

        var seeder = CreateSeeder(context, "admin", "same-password");

        await seeder.SeedAsync();

        var stored = await context.Admins.SingleAsync();
        // BCrypt hashes are salted, so an unchanged hash is the strongest
        // possible proof nothing was re-hashed, not just "equal" by
        // coincidence.
        Assert.Equal(realHash, stored.PasswordHash);
        _authService.Verify(a => a.HashPassword(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SeedAsync_OtherAdminsExist_AreLeftCompletelyAlone()
    {
        using var context = NewInMemoryContext();
        var otherHash = BCrypt.Net.BCrypt.HashPassword("someone-elses-password");
        context.Admins.Add(new Admin { Username = "someone-else", PasswordHash = otherHash });
        await context.SaveChangesAsync();

        _authService.Setup(a => a.HashPassword(It.IsAny<string>())).Returns("hashed-value");
        var seeder = CreateSeeder(context, "admin", "a-password");

        await seeder.SeedAsync();

        Assert.Equal(2, await context.Admins.CountAsync());
        var untouched = await context.Admins.SingleAsync(a => a.Username == "someone-else");
        Assert.Equal(otherHash, untouched.PasswordHash);
    }
}
