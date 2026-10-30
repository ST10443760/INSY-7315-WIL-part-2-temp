using Microsoft.EntityFrameworkCore;
using ThriveWellness.Data;
using ThriveWellness.Models;

namespace ThriveWellness.Tests;

// The card number, expiry and CVC must never be stored. These tests fail if
// anyone adds somewhere to keep them, on the transaction itself or on any
// other table.
public class PaymentTransactionSecurityTests
{
    private static readonly string[] ForbiddenNameParts =
    {
        "CardNumber", "Number", "Pan", "Expiry", "Expiration", "ExpMonth", "ExpYear", "Cvc", "Cvv", "SecurityCode"
    };

    [Fact]
    public void PaymentTransaction_HasNoPropertyThatCouldHoldTheFullCard()
    {
        var offending = typeof(PaymentTransaction).GetProperties()
            .Where(p => ForbiddenNameParts.Any(part => p.Name.Contains(part, StringComparison.OrdinalIgnoreCase)))
            .Select(p => p.Name)
            .ToList();

        Assert.Empty(offending);
    }

    [Fact]
    public void PaymentTransaction_HasOnlyTheExpectedProperties()
    {
        // An explicit allow-list, so a new column has to be added here on
        // purpose (and thought about) rather than slipping in.
        var expected = new[]
        {
            "PaymentTransactionId", "PaymentId", "Amount", "Currency", "Status", "Brand", "Last4",
            "GatewayReference", "FailureCode", "FailureMessage", "CreatedAt"
        };

        var actual = typeof(PaymentTransaction).GetProperties().Select(p => p.Name).OrderBy(n => n);

        Assert.Equal(expected.OrderBy(n => n), actual);
    }

    [Fact]
    public void NoEntityInTheDatabaseModel_HasAColumnForCardData()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        using var context = new ApplicationDbContext(options);

        var cardColumns = context.Model.GetEntityTypes()
            .SelectMany(e => e.GetProperties().Select(p => $"{e.ClrType.Name}.{p.Name}"))
            .Where(name => ForbiddenNameParts
                .Where(part => part != "Number")
                .Any(part => name.Split('.')[1].Contains(part, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        Assert.Empty(cardColumns);
    }

    [Fact]
    public void CardDetails_IsNotPartOfTheDatabaseModel()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        using var context = new ApplicationDbContext(options);

        Assert.Null(context.Model.FindEntityType(typeof(CardDetails)));
        Assert.Null(context.Model.FindEntityType(typeof(GatewayChargeRequest)));
    }
}
