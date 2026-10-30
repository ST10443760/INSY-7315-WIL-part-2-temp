using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ThriveWellness.Data;
using ThriveWellness.Models;
using ThriveWellness.Repositories.Implementations;
using ThriveWellness.Services.Implementations;

namespace ThriveWellness.Tests;

public class ClientAccountServiceTests
{
    private readonly ApplicationDbContext _context;
    private readonly ClientAccountService _service;

    public ClientAccountServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new ApplicationDbContext(options);
        _service = new ClientAccountService(new ClientRepository(_context), NullLogger<ClientAccountService>.Instance);
    }

    private static GoogleProfile Profile(string subject = "google-sub-1", string email = "jane@example.com", bool verified = true, string name = "Jane Doe") => new()
    {
        Subject = subject,
        Email = email,
        EmailVerified = verified,
        Name = name
    };

    private Client AddClient(string email, string? googleSubject = null, string name = "Existing Client")
    {
        var client = new Client
        {
            FullName = name,
            Email = email,
            PhoneNumber = "082 000 0000",
            GoogleSubject = googleSubject,
            CreatedAt = DateTime.UtcNow.AddMonths(-3)
        };
        _context.Clients.Add(client);
        _context.SaveChanges();
        return client;
    }

    [Fact]
    public async Task SignIn_ExistingGoogleSubject_ReturnsThatClient()
    {
        var existing = AddClient("jane@example.com", googleSubject: "google-sub-1");

        var result = await _service.SignInWithGoogleAsync(Profile());

        Assert.True(result.Success);
        Assert.Equal(existing.ClientId, result.Client!.ClientId);
        Assert.Equal(1, await _context.Clients.CountAsync());
    }

    [Fact]
    public async Task SignIn_ExistingGoogleSubject_WinsEvenIfTheEmailChanged()
    {
        var existing = AddClient("old-address@example.com", googleSubject: "google-sub-1");

        var result = await _service.SignInWithGoogleAsync(Profile(email: "new-address@example.com"));

        Assert.Equal(existing.ClientId, result.Client!.ClientId);
    }

    [Fact]
    public async Task SignIn_ExistingClientByEmail_LinksIgnoringCaseAndKeepsTheirDetails()
    {
        // Booked before sign-in existed, typing their address in mixed case.
        var existing = AddClient("Jane.Doe@Example.com", name: "Jane from the old form");

        var result = await _service.SignInWithGoogleAsync(Profile(email: "jane.doe@example.com"));

        Assert.True(result.Success);
        Assert.Equal(existing.ClientId, result.Client!.ClientId);
        var stored = await _context.Clients.SingleAsync();
        Assert.Equal("google-sub-1", stored.GoogleSubject);
        Assert.Equal("Jane from the old form", stored.FullName);
        Assert.Equal("082 000 0000", stored.PhoneNumber);
    }

    [Fact]
    public async Task SignIn_BrandNewClient_IsCreatedFromTheGoogleProfile()
    {
        var result = await _service.SignInWithGoogleAsync(Profile(subject: "new-sub", email: "new@example.com", name: "New Person"));

        Assert.True(result.Success);
        var created = await _context.Clients.SingleAsync();
        Assert.Equal(created.ClientId, result.Client!.ClientId);
        Assert.Equal("new-sub", created.GoogleSubject);
        Assert.Equal("New Person", created.FullName);
        Assert.Equal("new@example.com", created.Email);
        Assert.Equal(string.Empty, created.PhoneNumber);
        Assert.True(created.IsNew);
    }

    [Fact]
    public async Task SignIn_UnverifiedEmail_IsRejectedAndNothingIsLinkedOrCreated()
    {
        AddClient("jane@example.com");

        var result = await _service.SignInWithGoogleAsync(Profile(verified: false));

        Assert.False(result.Success);
        Assert.Null(result.Client);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
        var stored = await _context.Clients.SingleAsync();
        Assert.Null(stored.GoogleSubject);
    }

    [Fact]
    public async Task SignIn_Twice_DoesNotCreateADuplicate()
    {
        var first = await _service.SignInWithGoogleAsync(Profile(subject: "new-sub", email: "new@example.com"));
        var second = await _service.SignInWithGoogleAsync(Profile(subject: "new-sub", email: "new@example.com"));

        Assert.Equal(first.Client!.ClientId, second.Client!.ClientId);
        Assert.Equal(1, await _context.Clients.CountAsync());
    }

    [Fact]
    public async Task SignIn_EmailAlreadyLinkedToAnotherGoogleAccount_IsRejected()
    {
        AddClient("jane@example.com", googleSubject: "someone-elses-sub");

        var result = await _service.SignInWithGoogleAsync(Profile(subject: "google-sub-1"));

        Assert.False(result.Success);
        Assert.Equal("someone-elses-sub", (await _context.Clients.SingleAsync()).GoogleSubject);
    }

    [Fact]
    public async Task SignIn_MissingEmail_IsRejected()
    {
        var result = await _service.SignInWithGoogleAsync(Profile(email: ""));

        Assert.False(result.Success);
        Assert.Equal(0, await _context.Clients.CountAsync());
    }
}
