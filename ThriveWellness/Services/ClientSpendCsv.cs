using System.Globalization;
using System.Text;
using ThriveWellness.Models;

namespace ThriveWellness.Services
{
    // The Revenue page's "spend per client" table as a CSV file. Every field
    // is quoted, and a field starting with =, +, - or @ gets a leading
    // apostrophe so a client's name can never run as a spreadsheet formula.
    public static class ClientSpendCsv
    {
        public static string Build(IEnumerable<ClientSpendRow> clients)
        {
            var csv = new StringBuilder();
            csv.AppendLine("Client,Email,Payments,Total spent (ZAR),Last payment (class date)");
            foreach (var client in clients)
            {
                csv.AppendLine(string.Join(",",
                    Field(client.Name),
                    Field(client.Email),
                    Field(client.PaymentsCount.ToString(CultureInfo.InvariantCulture)),
                    Field(Money.ForCsv(client.TotalSpent)),
                    Field(client.LastPaymentDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))));
            }
            return csv.ToString();
        }

        private static string Field(string value)
        {
            if (value.Length > 0 && "=+-@".Contains(value[0]))
            {
                value = "'" + value;
            }
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
    }
}
