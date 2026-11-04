using System.Globalization;

namespace CatchIt.App;

/// <summary>Text formatting shared by the screens, so a price or a tier reads the same everywhere.</summary>
internal static class Display
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    /// <summary>A price such as <c>$1,299.50</c>.</summary>
    public static string Money(double price) => "$" + price.ToString("N2", Invariant);

    /// <summary>A date such as <c>Nov 28, 2025</c>.</summary>
    public static string Date(DateOnly date) => date.ToString("MMM d, yyyy", Invariant);

    /// <summary>The name of a brand quality tier: 1 Budget, 2 Standard, 3 Premium.</summary>
    public static string TierName(int tier) => tier switch
    {
        1 => "Budget",
        2 => "Standard",
        3 => "Premium",
        _ => tier.ToString(Invariant),
    };
}
