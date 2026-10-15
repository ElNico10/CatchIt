using System.Globalization;

namespace CatchIt.Core.Pricing;

/// <summary>The price of one listing on one day.</summary>
/// <param name="Date">The day the price applied.</param>
/// <param name="Price">The price in US dollars.</param>
public record PricePoint(DateOnly Date, double Price);

/// <summary>
/// The absolute lowest and highest price a listing has ever had, with the day each happened.
/// </summary>
/// <remarks>
/// Weekly history samples can step over a short sale, so the extremes are tracked separately
/// from every price the system sees. They answer "which day was this cheapest?" (usually a
/// holiday sale) and "when is it most expensive?" (the high-demand season).
/// </remarks>
/// <param name="ListingId">The listing these figures belong to.</param>
/// <param name="LowPrice">The lowest price recorded.</param>
/// <param name="LowDate">The first day that lowest price was seen.</param>
/// <param name="HighPrice">The highest price recorded.</param>
/// <param name="HighDate">The first day that highest price was seen.</param>
public record PriceExtremes(long ListingId, double LowPrice, DateOnly LowDate, double HighPrice, DateOnly HighDate);

/// <summary>
/// Converts dates to and from the text format stored in the database. ISO dates sort the same
/// as text and as dates, which is what lets SQLite use an index for date ranges.
/// </summary>
internal static class DateText
{
    private const string Format = "yyyy-MM-dd";

    public static string ToDb(DateOnly date) => date.ToString(Format, CultureInfo.InvariantCulture);

    public static DateOnly FromDb(string text) => DateOnly.ParseExact(text, Format, CultureInfo.InvariantCulture);
}
