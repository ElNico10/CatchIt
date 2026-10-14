using CatchIt.Core.Data;
using CatchIt.Core.Search;
using Microsoft.Data.Sqlite;

namespace CatchIt.Tests;

/// <summary>
/// Tests for <see cref="BulkImporter"/>: loading many products at once, safe re-runs, rejecting
/// bad rows, and leaving the database untouched if an import fails halfway.
/// Each test gets its own throwaway database file.
/// </summary>
public class BulkImporterTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 10, 12);

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"catchit-test-{Guid.NewGuid():N}.db");
    private readonly Database _db;
    private readonly BulkImporter _importer;

    public BulkImporterTests()
    {
        _db = new Database(_path);
        _importer = new BulkImporter(_db, today: Today);
    }

    public void Dispose()
    {
        // SQLite keeps pooled connections open, which would stop the file being deleted.
        SqliteConnection.ClearAllPools();
        if (File.Exists(_path)) File.Delete(_path);
    }

    /// <summary>Makes a batch of valid products with predictable names ("Importable Gadget 0", ...).</summary>
    private static List<ImportedProduct> Batch(int count, string brandPrefix = "Brand") =>
        Enumerable.Range(0, count)
            .Select(i => new ImportedProduct($"Importable Gadget {i}", $"{brandPrefix}{i % 5}", 1 + i % 3, "gadget electronics", 20 + i % 90))
            .ToList();

    private long Count(string table) => TestCatalog.Count(_db, $"SELECT COUNT(*) FROM {table}");

    // ---- Importing ----

    [Fact]
    public void Import_AddsProductsListingsAndCompactHistory()
    {
        var summary = _importer.Import(Batch(300));

        Assert.Equal(300, summary.ProductsAdded);
        Assert.Equal(0, summary.ProductsSkipped);
        Assert.Equal(0, summary.ProductsRejected);
        Assert.Equal(300, Count("Products"));
        Assert.Equal(summary.ListingsAdded, Count("Listings"));
        Assert.Equal(summary.HistoryPointsAdded, Count("PriceHistory"));
        Assert.Equal(summary.ListingsAdded, Count("PriceExtremes"));
        Assert.True(summary.ListingsAdded >= 300); // every product is on at least one marketplace
        Assert.True(summary.Elapsed > TimeSpan.Zero);
    }

    [Fact]
    public void Import_StoresAboutOneWeeklyPointPerListing_NotADailyOne()
    {
        var summary = _importer.Import(Batch(200));

        var pointsPerListing = (double)summary.HistoryPointsAdded / summary.ListingsAdded;
        Assert.InRange(pointsPerListing, 55, 62);   // 400 days is about 58 weeks, plus the low and high days
    }

    [Fact]
    public void Import_ProductsAreSearchableByNameAndByBrand()
    {
        _importer.Import(new[] { new ImportedProduct("Zorblax 100 Speaker", "Quuxco", 2, "audio", 59.99) });
        var search = new ProductSearchService(_db);

        Assert.Single(search.Search("zorblax").Hits);
        Assert.Single(search.Search("quux").Hits);   // a brand-only search finds it too
        Assert.Single(search.Search("audio").Hits);
    }

    [Fact]
    public void Import_ReusesBrands_AndTheFirstTierWins()
    {
        _importer.Import(new[]
        {
            new ImportedProduct("One", "SameBrand", 3, "", 10),
            new ImportedProduct("Two", "samebrand", 1, "", 10),   // same brand, different case and tier
        });

        Assert.Equal(1, Count("Brands"));
        Assert.Equal(3, TestCatalog.Count(_db, "SELECT QualityTier FROM Brands"));
    }

    [Fact]
    public void Import_IntoAnEmptyDatabase_CreatesTheDemoMarketplaces()
    {
        _importer.Import(Batch(5));

        Assert.Equal(4, Count("Marketplaces")); // the four demo marketplaces
    }

    [Fact]
    public void Import_AfterTheDemoSeed_UsesTheExistingMarketplaces()
    {
        new Seeder(_db).Run();
        var before = Count("Marketplaces");

        _importer.Import(Batch(20));

        Assert.Equal(before, Count("Marketplaces"));
    }

    [Fact]
    public void Import_HistoryDaysOption_ShrinksTheHistory()
    {
        var shortImporter = new BulkImporter(_db, historyDays: 60, today: Today);

        var summary = shortImporter.Import(Batch(100));

        // 61 days is 9 weekly points, plus at most the low and high days.
        Assert.True((double)summary.HistoryPointsAdded / summary.ListingsAdded <= 11);
    }

    [Fact]
    public void Import_SameSeed_GivesTheSameData()
    {
        var otherPath = Path.Combine(Path.GetTempPath(), $"catchit-test-{Guid.NewGuid():N}.db");
        try
        {
            var other = new Database(otherPath);
            new BulkImporter(other, today: Today).Import(Batch(50));
            _importer.Import(Batch(50));

            const string sql = "SELECT CAST(SUM(CurrentPrice) * 100 AS INTEGER) FROM Listings";
            Assert.Equal(TestCatalog.Count(other, sql), TestCatalog.Count(_db, sql));
            Assert.Equal(TestCatalog.Count(other, "SELECT COUNT(*) FROM PriceHistory"), Count("PriceHistory"));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(otherPath)) File.Delete(otherPath);
        }
    }

    // ---- Safe re-runs and bad rows ----

    [Fact]
    public void Import_RunTwice_SkipsExistingProductsAndAddsNothing()
    {
        _importer.Import(Batch(100));
        var tables = new[] { "Products", "Listings", "PriceHistory", "PriceExtremes", "Brands" };
        var before = tables.Select(Count).ToArray();

        var second = _importer.Import(Batch(100));

        Assert.Equal(0, second.ProductsAdded);
        Assert.Equal(100, second.ProductsSkipped);
        Assert.Equal(before, tables.Select(Count).ToArray());
    }

    [Fact]
    public void Import_RejectsInvalidRows_AndImportsTheRest()
    {
        var rows = new List<ImportedProduct>
        {
            new("Good Product", "GoodBrand", 2, "ok", 10),
            new("", "GoodBrand", 2, "", 10),            // no name
            new("   ", "GoodBrand", 2, "", 10),         // blank name
            new("No Brand", "", 2, "", 10),             // no brand
            new("Tier Zero", "GoodBrand", 0, "", 10),   // tier too low
            new("Tier Four", "GoodBrand", 4, "", 10),   // tier too high
            new("Free Item", "GoodBrand", 2, "", 0),    // price not positive
            new("Negative", "GoodBrand", 2, "", -5),
            new("Not A Number", "GoodBrand", 2, "", double.NaN),
            new("Infinite", "GoodBrand", 2, "", double.PositiveInfinity),
        };

        var summary = _importer.Import(rows);

        Assert.Equal(1, summary.ProductsAdded);
        Assert.Equal(9, summary.ProductsRejected);
        Assert.Equal(1, Count("Products"));
    }

    // ---- Search index and failure handling ----

    [Fact]
    public void Import_LeavesTheSearchIndexInStepForLaterChanges()
    {
        _importer.Import(Batch(10));

        TestCatalog.AddProduct(_db, "Added After Import Lamp", "Lumo", 2, "lighting");

        Assert.Single(new ProductSearchService(_db).Search("lamp").Hits);
    }

    [Fact]
    public void Import_WhenItFailsPartway_ChangesNothingAndKeepsSearchWorking()
    {
        _importer.Import(Batch(20));
        var productsBefore = Count("Products");
        var listingsBefore = Count("Listings");

        IEnumerable<ImportedProduct> FailingSource()
        {
            for (var i = 0; i < 5; i++)
                yield return new ImportedProduct($"Doomed Gadget {i}", "DoomBrand", 2, "doomed", 30);
            throw new InvalidOperationException("The source file broke.");
        }

        Assert.Throws<InvalidOperationException>(() => _importer.Import(FailingSource()));

        Assert.Equal(productsBefore, Count("Products"));
        Assert.Equal(listingsBefore, Count("Listings"));
        Assert.Equal(0, Count("Brands WHERE Name = 'DoomBrand'"));

        // The row-by-row search triggers were dropped during the import; the rollback must restore them.
        Assert.Equal(1, TestCatalog.Count(_db,
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'trigger' AND name = 'Products_search_insert'"));
        var search = new ProductSearchService(_db);
        Assert.Equal(0, search.Search("doomed").TotalMatches);
        Assert.Equal(20, search.Search("gadget").TotalMatches);
        TestCatalog.AddProduct(_db, "Post Failure Lamp", "Lumo", 2, "lighting");
        Assert.Single(search.Search("lamp").Hits);
    }

    [Fact]
    public void Import_EmptyInput_Succeeds()
    {
        var summary = _importer.Import(Array.Empty<ImportedProduct>());

        Assert.Equal(0, summary.ProductsAdded);
        Assert.Equal(0, Count("Products"));
    }
}
