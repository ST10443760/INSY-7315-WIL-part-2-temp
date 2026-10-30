using System.Globalization;

namespace ThriveWellness.Services
{
    // Formats rand amounts the same way on every machine: "R1 234.00" -
    // a non-breaking space between thousands (so an amount never wraps
    // onto two lines) and a full stop before the cents. Without a fixed
    // format the server's own culture decides, which showed
    // "R120,00" on one machine and "R120.00" on another.
    public static class Money
    {
        private static readonly NumberFormatInfo RandFormat = new()
        {
            NumberGroupSeparator = " ",
            NumberDecimalSeparator = ".",
            NumberGroupSizes = new[] { 3 },
            NegativeSign = "-"
        };

        public static string Format(decimal amount)
        {
            var digits = Math.Abs(amount).ToString("N2", RandFormat);
            return amount < 0 ? $"-R{digits}" : $"R{digits}";
        }

        // Plain "1234.00", for CSV files a spreadsheet will read as a number.
        public static string ForCsv(decimal amount) => amount.ToString("0.00", CultureInfo.InvariantCulture);
    }
}
