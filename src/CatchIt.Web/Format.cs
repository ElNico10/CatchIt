using System.Globalization;

namespace CatchIt.Web;

/// <summary>Text formats shared by the pages.</summary>
public static class Format
{
    private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");

    /// <summary>A price such as $1,234.50, or "No price" when there is none.</summary>
    public static string Money(double? price) => price is { } p ? p.ToString("C", Us) : "No price";

    /// <summary>A date such as Nov 20, 2026.</summary>
    public static string Date(DateOnly date) => date.ToString("MMM d, yyyy", Us);

    /// <summary>A fraction as a signed percentage: -0.12 becomes "-12%".</summary>
    public static string Percent(double? fraction) => fraction is { } f ? f.ToString("+0%;-0%;0%", Us) : "n/a";

    /// <summary>A brand quality tier as a word.</summary>
    public static string Tier(int tier) => tier switch { 1 => "Budget", 2 => "Standard", 3 => "Premium", _ => "Unknown" };
}
