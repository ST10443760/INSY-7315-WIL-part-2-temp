using ThriveWellness.Models;
using ThriveWellness.Services;

namespace ThriveWellness.Tests;

public class ClientSpendCsvTests
{
    [Fact]
    public void Build_WritesAHeaderAndOneQuotedRowPerClient()
    {
        var csv = ClientSpendCsv.Build(new[]
        {
            new ClientSpendRow { Name = "Jane Doe", Email = "jane@example.com", PaymentsCount = 3, TotalSpent = 1234.5m, LastPaymentDate = new DateTime(2026, 10, 21) }
        });

        var lines = csv.TrimEnd().Split(Environment.NewLine);
        Assert.Equal("Client,Email,Payments,Total spent (ZAR),Last payment (class date)", lines[0]);
        Assert.Equal("\"Jane Doe\",\"jane@example.com\",\"3\",\"1234.50\",\"2026-10-21\"", lines[1]);
    }

    [Fact]
    public void Build_EscapesQuotesAndNeutralisesFormulas()
    {
        var csv = ClientSpendCsv.Build(new[]
        {
            new ClientSpendRow { Name = "=HYPERLINK(\"x\")", Email = "a\"b@example.com", LastPaymentDate = new DateTime(2026, 1, 1) }
        });

        Assert.Contains("\"'=HYPERLINK(\"\"x\"\")\"", csv);
        Assert.Contains("\"a\"\"b@example.com\"", csv);
    }
}
