namespace CatchIt.Core.Pricing;

/// <summary>
/// Creates believable but SIMULATED daily price histories for the demo data: a slowly drifting
/// price with small daily noise, a pre-holiday demand bump in December, and sale windows that
/// differ by marketplace (the big November sales, a summer sale, and so on).
/// </summary>
/// <remarks>
/// The same random generator and inputs always produce the same series, so the demo data is
/// repeatable. Nothing here comes from a real marketplace.
/// </remarks>
public static class PriceHistoryGenerator
{
    /// <summary>A yearly sale: where it starts, how long it lasts, how likely a listing joins, how deep it cuts.</summary>
    private record SaleEvent(int Month, int Day, int Length, double Probability, double MinDepth, double MaxDepth);

    // One profile per marketplace, because each seller runs its sales on different dates.
    // Marketplaces beyond the fourth reuse the profiles in turn.
    private static readonly SaleEvent[][] Profiles =
    {
        new[] // marketplace 0: long November sale, summer sale, back to school, New Year
        {
            new SaleEvent(11, 21, 10, 0.85, 0.15, 0.35),
            new SaleEvent(7, 8, 4, 0.60, 0.10, 0.25),
            new SaleEvent(8, 1, 20, 0.35, 0.05, 0.15),
            new SaleEvent(1, 2, 9, 0.40, 0.05, 0.15),
        },
        new[] // marketplace 1: short, deep Thanksgiving-weekend sale and a long New Year sale
        {
            new SaleEvent(11, 27, 4, 0.90, 0.20, 0.40),
            new SaleEvent(7, 15, 3, 0.40, 0.10, 0.20),
            new SaleEvent(1, 1, 14, 0.50, 0.05, 0.20),
        },
        new[] // marketplace 2: week-long November sale, post-Christmas clearance, summer sale
        {
            new SaleEvent(11, 24, 7, 0.70, 0.10, 0.30),
            new SaleEvent(12, 26, 6, 0.50, 0.10, 0.20),
            new SaleEvent(7, 10, 5, 0.50, 0.10, 0.20),
        },
        new[] // marketplace 3: Cyber Monday focus, back to school, spring sale
        {
            new SaleEvent(11, 29, 4, 0.75, 0.15, 0.30),
            new SaleEvent(8, 10, 10, 0.40, 0.05, 0.15),
            new SaleEvent(3, 15, 7, 0.30, 0.05, 0.15),
        },
    };

    /// <summary>Generates one price per day, oldest first, ending on <paramref name="endDate"/>.</summary>
    /// <param name="basePrice">The typical price before drift, demand and sales. Must be positive.</param>
    /// <param name="marketplaceIndex">Which marketplace's sale calendar to use.</param>
    /// <param name="endDate">The last day of the series (normally today).</param>
    /// <param name="days">How many days before <paramref name="endDate"/> to start; the series has days + 1 points.</param>
    /// <param name="rng">Source of randomness; pass a seeded one for repeatable data.</param>
    /// <returns>The daily series, oldest first, with every price at least $1.00.</returns>
    public static IReadOnlyList<PricePoint> Generate(
        double basePrice, int marketplaceIndex, DateOnly endDate, int days, Random rng)
    {
        if (!(basePrice > 0)) throw new ArgumentOutOfRangeException(nameof(basePrice), "The base price must be positive.");
        if (days < 0) throw new ArgumentOutOfRangeException(nameof(days), "Days cannot be negative.");

        var count = days + 1;
        var start = endDate.AddDays(-days);
        var profile = Profiles[((marketplaceIndex % Profiles.Length) + Profiles.Length) % Profiles.Length];

        // The multiplier each day gets from sales; 1.0 means no sale. Overlapping sales take the deeper cut.
        var sale = Enumerable.Repeat(1.0, count).ToArray();

        void ApplySale(DateOnly from, int length, double depth)
        {
            for (var i = 0; i < length; i++)
            {
                var index = from.AddDays(i).DayNumber - start.DayNumber;
                if (index >= 0 && index < count)
                    sale[index] = Math.Min(sale[index], 1 - depth);
            }
        }

        // Scheduled sales: each year, each event in this marketplace's calendar may run for this listing.
        for (var year = start.Year; year <= endDate.Year; year++)
        {
            foreach (var ev in profile)
            {
                if (rng.NextDouble() >= ev.Probability) continue;
                var depth = ev.MinDepth + rng.NextDouble() * (ev.MaxDepth - ev.MinDepth);
                ApplySale(new DateOnly(year, ev.Month, ev.Day), ev.Length, depth);
            }
        }

        // A few unscheduled flash sales a year, so not every dip lands on a holiday.
        var flashSales = Math.Max(1, (int)Math.Round(count / 365.0 * rng.Next(1, 4)));
        for (var i = 0; i < flashSales; i++)
            ApplySale(start.AddDays(rng.Next(count)), rng.Next(2, 6), 0.08 + rng.NextDouble() * 0.12);

        // Demand: prices creep up before Christmas. How much differs between products.
        var decemberLift = 0.04 + rng.NextDouble() * 0.04;

        // Prices drift between about -9% and +2% a year, and wobble slightly day to day.
        var drift = -0.00025 + rng.NextDouble() * 0.0003;
        var level = 1.0;

        var series = new List<PricePoint>(count);
        for (var i = 0; i < count; i++)
        {
            var date = start.AddDays(i);
            level = Math.Clamp(level * (1 + drift) * (1 + Gaussian(rng) * 0.004), 0.7, 1.3);
            var demand = date.Month == 12 && date.Day <= 24 ? 1 + decemberLift : 1.0;
            var price = Math.Max(1.0, Math.Round(basePrice * level * demand * sale[i], 2));
            series.Add(new PricePoint(date, price));
        }
        return series;
    }

    /// <summary>A normally distributed random number with mean 0 and standard deviation 1 (Box-Muller).</summary>
    private static double Gaussian(Random rng)
    {
        var u1 = 1.0 - rng.NextDouble(); // avoids log(0)
        var u2 = rng.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }
}
