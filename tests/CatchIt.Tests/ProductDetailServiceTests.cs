using CatchIt.Core.Data;
using CatchIt.Core.Search;
using Microsoft.Data.Sqlite;

namespace CatchIt.Tests;

/// <summary>
/// Tests for <see cref="ProductDetailService"/>: the details of a listing and how the three alternative
/// slots (cheapest option, better reviewed, may drop soon) are filled from the stored alternatives.
/// </summary>
public class ProductDetailServiceTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 11, 3);
    private static readonly DateOnly BlackFridayLastYear = new(2025, 11, 28);

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"catchit-test-{Guid.NewGuid():N}.db");
    private readonly Database _db;
    private readonly ProductDetailService _service;

    public ProductDetailServiceTests()
    {
        _db = new Database(_path);
        _db.Initialize();
        _service = new ProductDetailService(_db, Today);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_path)) File.Delete(_path);
    }

    private void Sql(string sql, params (string, object?)[] args)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (k, v) in args) cmd.Parameters.AddWithValue(k, v ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    /// <summary>Adds a product with one listing; returns (product id, listing id).</summary>
    private (long Product, long Listing) Add(string name, string brand, int tier, double? price,
        double? rating = null, int? ratingCount = null)
    {
        var product = TestCatalog.AddProduct(_db, name, brand, tier);
        Sql("UPDATE Products SET AverageRating = $r, RatingCount = $c WHERE Id = $p",
            ("$r", rating), ("$c", ratingCount), ("$p", product));
        return (product, TestCatalog.AddListing(_db, product, "Shop", price));
    }

    private void Link(long product, long alternative) =>
        Sql("INSERT INTO Alternatives (ProductId, AlternativeProductId) VALUES ($p, $a)", ("$p", product), ("$a", alternative));

    /// <summary>The scenario: a premium $100 product with four stored alternatives.</summary>
    private (long Main, long Cheap, long Loved, long Fake, long Dipper) Scenario()
    {
        var main = Add("Main Headphones", "PremiumCo", 3, 100, rating: 3.5, ratingCount: 200);
        var cheap = Add("Cheap Headphones", "BudgetCo", 1, 40, rating: 3.0, ratingCount: 50);
        var loved = Add("Loved Headphones", "MidCo", 2, 120, rating: 4.8, ratingCount: 500);   // dearer, but well reviewed
        var fake = Add("Fake Rated Headphones", "ShadyCo", 3, 150, rating: 5.0, ratingCount: 3); // too few ratings to trust
        var dipper = Add("Dipper Headphones", "DipCo", 3, 100);                                // no cheaper or lower tier, no rating
        foreach (var alt in new[] { cheap, loved, fake, dipper }) Link(main.Product, alt.Product);

        // The dipper cost $60 around Black Friday last year (a 40% drop); the cheap one only dipped 5%.
        TestCatalog.AddPoint(_db, dipper.Listing, BlackFridayLastYear, 60);
        TestCatalog.AddPoint(_db, cheap.Listing, BlackFridayLastYear, 38);
        return (main.Listing, cheap.Listing, loved.Listing, fake.Listing, dipper.Listing);
    }

    private static string[] Names(ProductDetail d) => d.Alternatives.Select(a => a.ProductName).ToArray();

    // ---- The listing itself ----

    [Fact]
    public void GetDetail_ReturnsNullForAnUnknownListing() => Assert.Null(_service.GetDetail(999));

    [Fact]
    public void GetDetail_ReturnsTheListingItsHistoryAndTheDerivedFigures()
    {
        var item = Add("Widget", "AcmeCo", 2, 80, rating: 4.2, ratingCount: 321);
        Sql("UPDATE Products SET Category = 'Gadgets > Widgets' WHERE Id = $p", ("$p", item.Product));
        for (var i = 1; i <= 10; i++) TestCatalog.AddPoint(_db, item.Listing, Today.AddDays(-7 * i), 100);
        Sql("INSERT INTO PriceExtremes (ListingId, LowPrice, LowDate, HighPrice, HighDate) VALUES ($l, 70, '2026-03-01', 120, '2025-12-10')",
            ("$l", item.Listing));

        var d = _service.GetDetail(item.Listing)!;

        Assert.Equal("Widget", d.Listing.ProductName);
        Assert.Equal("AcmeCo", d.Listing.BrandName);
        Assert.Equal(80, d.Listing.CurrentPrice);
        Assert.Equal("Gadgets > Widgets", d.Category);
        Assert.Equal(4.2, d.ProductRating);
        Assert.Equal(321, d.RatingCount);
        Assert.Equal(100, d.Average90);
        Assert.True(d.Listing.OnSale);                                   // 20% below its average
        Assert.Equal(10, d.History.Count);
        Assert.True(d.History.Zip(d.History.Skip(1), (a, b) => a.Date < b.Date).All(x => x)); // oldest first
        Assert.Equal(70, d.Extremes!.LowPrice);
        Assert.Equal(new DateOnly(2025, 12, 10), d.Extremes.HighDate);
    }

    [Fact]
    public void GetDetail_DoesNotFlagOnSaleWithoutEnoughHistory()
    {
        var item = Add("Thin History Widget", "AcmeCo", 2, 10);
        TestCatalog.AddPoint(_db, item.Listing, Today.AddDays(-7), 100);

        var d = _service.GetDetail(item.Listing)!;

        Assert.Null(d.Average90);
        Assert.False(d.Listing.OnSale);
    }

    // ---- The alternative slots ----

    [Fact]
    public void Alternatives_FillTheThreeSlotsInOrder_WithTheirReasons()
    {
        var s = Scenario();

        var d = _service.GetDetail(s.Main)!;

        Assert.Equal(new[] { "Cheap Headphones", "Loved Headphones", "Dipper Headphones" }, Names(d));
        Assert.Equal(new[] { "Cheapest option" }, d.Alternatives[0].Tags);
        Assert.Equal(new[] { "Better reviewed" }, d.Alternatives[1].Tags);
        var drop = Assert.Single(d.Alternatives[2].Tags);
        Assert.Contains("May drop soon", drop);
        Assert.Contains("40%", drop);
        Assert.Contains("Black Friday / Cyber Monday", drop);
    }

    [Fact]
    public void Alternatives_ShowPriceTierMarketplaceAndRating()
    {
        var d = _service.GetDetail(Scenario().Main)!;
        var cheap = d.Alternatives[0];

        Assert.Equal(40, cheap.Price);
        Assert.Equal(1, cheap.QualityTier);
        Assert.Equal("BudgetCo", cheap.BrandName);
        Assert.Equal("Shop", cheap.MarketplaceName);
        Assert.Equal(3.0, cheap.Rating);
    }

    [Fact]
    public void Alternatives_TheCheapSlotNeedsBothCheaperAndLowerTier()
    {
        var main = Add("Main", "PremiumCo", 3, 100);
        var cheaperSameTier = Add("Cheaper Same Tier", "OtherPremium", 3, 50);
        var lowerTierButDearer = Add("Dearer Lower Tier", "BudgetCo", 1, 150);
        Link(main.Product, cheaperSameTier.Product);
        Link(main.Product, lowerTierButDearer.Product);

        Assert.Empty(_service.GetDetail(main.Listing)!.Alternatives);
    }

    [Fact]
    public void Alternatives_TheCheapSlotPicksTheLowestPrice()
    {
        var main = Add("Main", "PremiumCo", 3, 100);
        foreach (var (name, price) in new[] { ("A", 60.0), ("B", 30.0), ("C", 45.0) })
            Link(main.Product, Add($"Alt {name}", $"Brand{name}", 1, price).Product);

        var d = _service.GetDetail(main.Listing)!;

        Assert.Equal("Alt B", Assert.Single(d.Alternatives).ProductName);
    }

    [Fact]
    public void Alternatives_BetterReviewedIgnoresRatingsFromTooFewPeople()
    {
        var s = Scenario();

        var d = _service.GetDetail(s.Main)!;

        Assert.DoesNotContain("Fake Rated Headphones", Names(d));
    }

    [Fact]
    public void Alternatives_ADropBelowTenPercentIsNotShown()
    {
        var main = Add("Main", "PremiumCo", 3, 100);
        var alt = Add("Barely Dips", "SameTierCo", 3, 100);
        Link(main.Product, alt.Product);
        TestCatalog.AddPoint(_db, alt.Listing, BlackFridayLastYear, 95);

        Assert.Empty(_service.GetDetail(main.Listing)!.Alternatives);
    }

    [Fact]
    public void Alternatives_AProductThatWinsTwoSlotsAppearsOnceWithBothTags()
    {
        var s = Scenario();
        // Make the cheap one also the biggest dipper: 50% cheaper last Black Friday than now.
        TestCatalog.AddPoint(_db, s.Cheap, new DateOnly(2025, 11, 29), 20);

        var d = _service.GetDetail(s.Main)!;

        Assert.Equal(new[] { "Cheap Headphones", "Loved Headphones" }, Names(d));
        var cheap = d.Alternatives[0];
        Assert.Equal(2, cheap.Tags.Count);
        Assert.Equal("Cheapest option", cheap.Tags[0]);
        Assert.Contains("May drop soon", cheap.Tags[1]);
        Assert.Contains("50%", cheap.Tags[1]);
    }

    [Fact]
    public void Alternatives_NeverMoreThanThreeCards()
    {
        var d = _service.GetDetail(Scenario().Main)!;

        Assert.InRange(d.Alternatives.Count, 0, 3);
        Assert.Equal(d.Alternatives.Count, d.Alternatives.Select(a => a.ProductId).Distinct().Count());
    }

    [Fact]
    public void Alternatives_SkipOnesWithNoPricedListing()
    {
        var main = Add("Main", "PremiumCo", 3, 100);
        Link(main.Product, Add("Unpriced Alternative", "BudgetCo", 1, null).Product);

        Assert.Empty(_service.GetDetail(main.Listing)!.Alternatives);
    }

    [Fact]
    public void Alternatives_WithoutACurrentPrice_StillOfferTheOtherSlots()
    {
        var main = Add("Unpriced Main", "PremiumCo", 3, null);
        Link(main.Product, Add("Loved", "MidCo", 2, 120, rating: 4.8, ratingCount: 500).Product);
        Link(main.Product, Add("Cheap", "BudgetCo", 1, 10).Product);

        var d = _service.GetDetail(main.Listing)!;

        Assert.Equal(new[] { "Loved" }, Names(d));   // no price to be cheaper than, so no cheapest slot
    }

    [Fact]
    public void Alternatives_UseTheAlternativesCheapestListing()
    {
        var main = Add("Main", "PremiumCo", 3, 100);
        var alt = Add("Alt", "BudgetCo", 1, 80);
        Link(main.Product, alt.Product);
        TestCatalog.AddListing(_db, alt.Product, "Other Shop", 55);   // a second, cheaper marketplace

        var card = Assert.Single(_service.GetDetail(main.Listing)!.Alternatives);

        Assert.Equal(55, card.Price);
        Assert.Equal("Other Shop", card.MarketplaceName);
    }

    [Fact]
    public void Alternatives_AreEmptyWhenNoneAreStored()
    {
        var main = Add("Lonely", "AcmeCo", 2, 100);

        Assert.Empty(_service.GetDetail(main.Listing)!.Alternatives);
    }
}
