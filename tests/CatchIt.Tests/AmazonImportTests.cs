using System.IO.Compression;
using System.Text;
using CatchIt.Core.Data;
using Microsoft.Data.Sqlite;

namespace CatchIt.Tests;

/// <summary>
/// Tests for the dataset import: reading rows from the Amazon metadata format, upgrading old
/// databases, deriving brand tiers and alternatives, and the whole import from a file.
/// All data here is made up; the real dataset is never needed to run the tests.
/// </summary>
public class AmazonImportTests : IDisposable
{
    private static readonly DateOnly Today = new(2026, 10, 12);

    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"catchit-amazon-{Guid.NewGuid():N}.db");
    private readonly List<string> _files = new();
    private readonly Database _db;

    public AmazonImportTests() => _db = new Database(_dbPath);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
        foreach (var f in _files) if (File.Exists(f)) File.Delete(f);
    }

    private long Count(string sql) => TestCatalog.Count(_db, sql);

    private string WriteGz(params string[] lines)
    {
        var path = Path.Combine(Path.GetTempPath(), $"catchit-meta-{Guid.NewGuid():N}.jsonl.gz");
        _files.Add(path);
        using var file = File.Create(path);
        using var gz = new GZipStream(file, CompressionLevel.Fastest);
        gz.Write(Encoding.UTF8.GetBytes(string.Join('\n', lines) + "\n"));
        return path;
    }

    private static string Line(string title, string? brand, string? price, string categories = "[\"Electronics\",\"Headphones\"]",
        string rating = "4.5", string count = "100", string store = "null") =>
        $$"""{"main_category":"Electronics","title":"{{title}}","average_rating":{{rating}},"rating_number":{{count}},"price":{{price ?? "null"}},"store":{{store}},"categories":{{categories}},"details":{{(brand == null ? "{}" : $$"""{"Brand":"{{brand}}"}""")}}}""";

    // ---- Reading rows ----

    [Fact]
    public void ParseLine_ReadsAProduct()
    {
        var p = AmazonDatasetReader.ParseLine(Line("Acme Headphones", "Acme", "59.99"))!;

        Assert.Equal("Acme Headphones", p.Name);
        Assert.Equal("Acme", p.Brand);
        Assert.Equal(59.99, p.BasePrice);
        Assert.Equal(4.5, p.AverageRating);
        Assert.Equal(100, p.RatingCount);
        Assert.Equal("Electronics > Headphones", p.Category);    // the full path, so same-named leaves elsewhere are not mixed in
        Assert.Contains("Headphones", p.Keywords);
        Assert.Equal(AmazonDatasetReader.PlaceholderTier, p.QualityTier);
    }

    [Fact]
    public void ParseLine_GivesRowsWithoutCategoriesNoCategory() =>
        Assert.Equal("", AmazonDatasetReader.ParseLine(Line("X", "B", "5", categories: "[]"))!.Category);

    [Fact]
    public void ParseLine_FallsBackToTheStoreWhenThereIsNoBrand() =>
        Assert.Equal("Acme Store", AmazonDatasetReader.ParseLine(Line("X", null, "5", store: "\"Acme Store\""))!.Brand);

    [Fact]
    public void ParseLine_AcceptsTextPrices() =>
        Assert.Equal(1299.5, AmazonDatasetReader.ParseLine(Line("X", "B", "\"$1,299.50\""))!.BasePrice);

    [Theory]
    [InlineData(null)]          // no price
    [InlineData("\"None\"")]    // text that is not a price
    [InlineData("0")]
    [InlineData("-4")]
    public void ParseLine_SkipsRowsWithoutAUsablePrice(string? price) =>
        Assert.Null(AmazonDatasetReader.ParseLine(Line("X", "B", price)));

    [Fact]
    public void ParseLine_SkipsRowsWithoutATitleOrBrand()
    {
        Assert.Null(AmazonDatasetReader.ParseLine(Line("  ", "B", "5")));
        Assert.Null(AmazonDatasetReader.ParseLine(Line("X", null, "5")));      // no brand and no store
    }

    [Fact]
    public void ParseLine_SkipsDamagedLines()
    {
        Assert.Null(AmazonDatasetReader.ParseLine("{not json"));
        Assert.Null(AmazonDatasetReader.ParseLine("[1,2]"));
    }

    [Fact]
    public void ParseLine_IgnoresImpossibleRatingsAndCutsLongNames()
    {
        var p = AmazonDatasetReader.ParseLine(Line(new string('x', 500), "B", "5", rating: "9", count: "-1"))!;

        Assert.Null(p.AverageRating);
        Assert.Null(p.RatingCount);
        Assert.Equal(AmazonDatasetReader.MaxNameLength, p.Name.Length);
    }

    [Fact]
    public void Read_StreamsAGzippedFile_AndCountsWhatItSkipped()
    {
        var path = WriteGz(Line("A", "B", "5"), "{broken", Line("C", "B", null), Line("D", "B", "7"));
        var reader = new AmazonDatasetReader();

        var products = reader.Read(path).ToList();

        Assert.Equal(new[] { "A", "D" }, products.Select(p => p.Name));
        Assert.Equal(4, reader.RowsRead);
        Assert.Equal(2, reader.RowsSkipped);
    }

    // ---- Database upgrade ----

    [Fact]
    public void OpeningAnOlderDatabase_AddsTheNewProductColumnsAndKeepsItsProducts()
    {
        using (var conn = new SqliteConnection($"Data Source={_dbPath};Pooling=False"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE Brands (Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL UNIQUE, QualityTier INTEGER NOT NULL);
                CREATE TABLE Products (Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL UNIQUE,
                    Keywords TEXT NOT NULL DEFAULT '', BrandId INTEGER NOT NULL REFERENCES Brands(Id));
                INSERT INTO Brands (Name, QualityTier) VALUES ('Old', 2);
                INSERT INTO Products (Name, BrandId) VALUES ('Old Product', 1);
                """;
            cmd.ExecuteNonQuery();
        }

        _db.Initialize();

        Assert.Equal(4, Count("SELECT COUNT(*) FROM pragma_table_info('Products') WHERE name IN ('Category','TypicalPrice','AverageRating','RatingCount')"));
        Assert.Equal(1, Count("SELECT COUNT(*) FROM Products WHERE Name = 'Old Product' AND Category = ''"));
    }

    // ---- Tiers and alternatives ----

    /// <summary>
    /// Thirty products in one category: three brands of ten products each, priced $10-19, $20-29 and $30-39.
    /// "Gadget Star" ($25, Mid) has a trusted 4.9 rating; "Gadget Fake" ($26, Mid) has a 5.0 rating from only 3 ratings.
    /// </summary>
    private void ImportThreePriceBands()
    {
        var products = new List<ImportedProduct>();
        for (var i = 0; i < 30; i++)
        {
            var brand = i < 10 ? "CheapCo" : i < 20 ? "MidCo" : "PriceyCo";
            double? rating = i == 15 ? 4.9 : i == 16 ? 5.0 : 3.5;
            int? count = i == 15 ? 200 : i == 16 ? 3 : 50;
            var name = i == 15 ? "Gadget Star" : i == 16 ? "Gadget Fake" : $"Gadget {i}";
            products.Add(new ImportedProduct(name, brand, 2, "gadget", 10 + i, rating, count, "Gadgets"));
        }
        new BulkImporter(_db, today: Today).Import(products);
    }

    private long Tier(string brand) => Count($"SELECT QualityTier FROM Brands WHERE Name = '{brand}'");

    [Fact]
    public void DeriveBrandTiers_RanksBrandsByWhereTheirPricesFallInTheCategory()
    {
        ImportThreePriceBands();

        var updated = CatalogEnricher.DeriveBrandTiers(_db);

        Assert.Equal(3, updated);
        Assert.Equal(1, Tier("CheapCo"));
        Assert.Equal(2, Tier("MidCo"));
        Assert.Equal(3, Tier("PriceyCo"));
    }

    [Fact]
    public void DeriveBrandTiers_LeavesBrandsWithTooFewProductsAlone()
    {
        ImportThreePriceBands();
        new BulkImporter(_db, today: Today).Import(new[] { new ImportedProduct("Lonely Gadget", "TinyCo", 3, "", 100, null, null, "Gadgets") });

        CatalogEnricher.DeriveBrandTiers(_db);

        Assert.Equal(3, Tier("TinyCo")); // one product is not enough evidence, so the given tier stays
    }

    [Fact]
    public void DeriveBrandTiers_IgnoresSmallCategories()
    {
        var few = Enumerable.Range(0, CatalogEnricher.MinCategorySize - 1)
            .Select(i => new ImportedProduct($"Rare {i}", "RareCo", 2, "", 10 + i, null, null, "Rare things"));
        new BulkImporter(_db, today: Today).Import(few);

        Assert.Equal(0, CatalogEnricher.DeriveBrandTiers(_db));
    }

    [Fact]
    public void BuildAlternatives_StoresOnlyCheaperLowerTierOrBetterRatedProductsOfOtherBrands()
    {
        ImportThreePriceBands();
        CatalogEnricher.DeriveBrandTiers(_db);

        CatalogEnricher.BuildAlternatives(_db);

        // Every link joins a product to another brand in the same category, and is either
        // (cheaper AND lower tier) or (better rated, trusted).
        Assert.Equal(0, Count("""
            SELECT COUNT(*) FROM Alternatives a
            JOIN Products p ON p.Id = a.ProductId JOIN Brands pb ON pb.Id = p.BrandId
            JOIN Products o ON o.Id = a.AlternativeProductId JOIN Brands ob ON ob.Id = o.BrandId
            WHERE o.Category <> p.Category OR o.BrandId = p.BrandId
               OR NOT ((o.TypicalPrice < p.TypicalPrice AND ob.QualityTier < pb.QualityTier)
                       OR (o.AverageRating > COALESCE(p.AverageRating, 0) AND o.RatingCount >= 10))
            """));
        Assert.True(Count("SELECT COUNT(*) FROM Alternatives") > 0);
    }

    [Fact]
    public void BuildAlternatives_OffersTheTrustedTopRatingButNeverOneBasedOnAFewRatings()
    {
        ImportThreePriceBands();
        CatalogEnricher.DeriveBrandTiers(_db);

        CatalogEnricher.BuildAlternatives(_db);

        // A cheap-brand product has the well-rated "Gadget Star" among its alternatives...
        Assert.Equal(1, Count("""
            SELECT COUNT(*) FROM Alternatives a JOIN Products p ON p.Id = a.ProductId JOIN Products o ON o.Id = a.AlternativeProductId
            WHERE p.Name = 'Gadget 5' AND o.Name = 'Gadget Star'
            """));
        // ...but "Gadget Fake" (5.0 from 3 ratings) is never suggested for being better rated.
        Assert.Equal(0, Count("SELECT COUNT(*) FROM Alternatives a JOIN Products o ON o.Id = a.AlternativeProductId WHERE o.Name = 'Gadget Fake'"));
    }

    [Fact]
    public void BuildAlternatives_CapsEachProductAndCanBeRunAgain()
    {
        ImportThreePriceBands();
        CatalogEnricher.DeriveBrandTiers(_db);

        var first = CatalogEnricher.BuildAlternatives(_db);
        var again = CatalogEnricher.BuildAlternatives(_db);

        Assert.True(first > 0);
        Assert.Equal(0, again);
        Assert.Equal(0, Count($"""
            SELECT COUNT(*) FROM (SELECT ProductId FROM Alternatives GROUP BY ProductId
                                  HAVING COUNT(*) > {CatalogEnricher.CheaperPicks + CatalogEnricher.BetterRatedPicks})
            """));
    }

    // ---- The whole import ----

    [Fact]
    public void Run_ImportsAFile_WithRatingsTiersListingsAndAlternatives()
    {
        var lines = Enumerable.Range(0, 30)
            .Select(i => Line($"Gizmo {i}", i < 10 ? "LowCo" : i < 20 ? "MidCo" : "HighCo", (10 + i).ToString(), "[\"Electronics\",\"Gizmos\"]", "4.0", "40"))
            .Append("{broken")
            .ToArray();
        var path = WriteGz(lines);

        var result = AmazonImporter.Run(_db, path, importer: new BulkImporter(_db, today: Today));

        Assert.Equal(30, result.Import.ProductsAdded);
        Assert.Equal(31, result.RowsRead);
        Assert.Equal(1, result.RowsSkipped);
        Assert.Equal(3, result.BrandsTiered);
        Assert.True(result.AlternativeLinks > 0);
        Assert.True(Count("SELECT COUNT(*) FROM Listings") >= 30);
        Assert.Equal(30, Count("SELECT COUNT(*) FROM Products WHERE AverageRating = 4.0 AND RatingCount = 40 AND Category = 'Electronics > Gizmos'"));
        Assert.Equal(1, Tier("LowCo"));
        Assert.Equal(3, Tier("HighCo"));
    }

    [Fact]
    public void Run_HonoursTheLimit_AndIsSafeToRepeat()
    {
        var path = WriteGz(Enumerable.Range(0, 30).Select(i => Line($"Gizmo {i}", "Co", (5 + i).ToString())).ToArray());

        Assert.Equal(12, AmazonImporter.Run(_db, path, 12, new BulkImporter(_db, today: Today)).Import.ProductsAdded);

        var second = AmazonImporter.Run(_db, path, 12, new BulkImporter(_db, today: Today));
        Assert.Equal(0, second.Import.ProductsAdded);
        Assert.Equal(12, second.Import.ProductsSkipped);
        Assert.Equal(12, Count("SELECT COUNT(*) FROM Products"));
    }
}
