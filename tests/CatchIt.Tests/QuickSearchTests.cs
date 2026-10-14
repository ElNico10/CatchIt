using CatchIt.Core.Data;
using CatchIt.Core.Search;
using Microsoft.Data.Sqlite;

namespace CatchIt.Tests;

/// <summary>
/// Tests for the quick (as-you-type) search mode, the async wrappers and sharing the service
/// between threads. Each test gets its own throwaway database file.
/// </summary>
public class QuickSearchTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"catchit-test-{Guid.NewGuid():N}.db");
    private readonly Database _db;
    private readonly ProductSearchService _search;

    public QuickSearchTests()
    {
        _db = new Database(_path);
        _db.Initialize();
        _search = new ProductSearchService(_db);

        TestCatalog.AddProduct(_db, "Apex Wireless Headphones", "Apex", 3, "headphones audio wireless");
        TestCatalog.AddProduct(_db, "Orbit Wireless Headphones", "Orbit", 2, "headphones audio wireless");
        TestCatalog.AddProduct(_db, "Zenith 4K Monitor 27in", "Zenith", 3, "monitor display screen 4k");
    }

    public void Dispose()
    {
        // SQLite keeps pooled connections open, which would stop the file being deleted.
        SqliteConnection.ClearAllPools();
        if (File.Exists(_path)) File.Delete(_path);
    }

    private void AddGadgets(int count)
    {
        for (var i = 0; i < count; i++)
            TestCatalog.AddProduct(_db, $"Test Gadget {i}", "GadgetCo", 1, "gadget");
    }

    // ---- The count cap ----

    [Fact]
    public void QuickSearch_BelowTheCap_ReportsAnExactTotal()
    {
        AddGadgets(30);

        var page = _search.QuickSearch("gadget", limit: 50, countCap: 100);

        Assert.Equal(30, page.TotalMatches);
        Assert.False(page.TotalIsCapped);
        Assert.Equal(30, page.Hits.Count);
    }

    [Fact]
    public void QuickSearch_ExactlyAtTheCap_IsStillExact()
    {
        AddGadgets(30);

        var page = _search.QuickSearch("gadget", limit: 50, countCap: 30);

        Assert.Equal(30, page.TotalMatches);
        Assert.False(page.TotalIsCapped);
    }

    [Fact]
    public void QuickSearch_OneOverTheCap_IsReportedAsCapped()
    {
        AddGadgets(30);

        var page = _search.QuickSearch("gadget", limit: 50, countCap: 29);

        Assert.Equal(29, page.TotalMatches);
        Assert.True(page.TotalIsCapped);
    }

    [Fact]
    public void QuickSearch_FarOverTheCap_StillShowsAFullPageOfHits()
    {
        AddGadgets(60);

        var page = _search.QuickSearch("gadget", limit: 10, countCap: 20);

        Assert.Equal(20, page.TotalMatches);
        Assert.True(page.TotalIsCapped);
        Assert.Equal(10, page.Hits.Count);
    }

    [Fact]
    public void QuickSearch_ADefaultCapOfOneThousand()
    {
        Assert.Equal(1000, ProductSearchService.DefaultCountCap);
        Assert.False(_search.QuickSearch("wireless").TotalIsCapped);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void QuickSearch_ACapBelowOneIsTreatedAsOne(int cap)
    {
        var page = _search.QuickSearch("wireless", countCap: cap);

        Assert.Equal(1, page.TotalMatches);
        Assert.True(page.TotalIsCapped); // two products match, so one is a cap
    }

    [Fact]
    public void Search_RankedMode_NeverReportsACappedTotal()
    {
        AddGadgets(60);

        var page = _search.Search("gadget", limit: 10);

        Assert.Equal(60, page.TotalMatches);
        Assert.False(page.TotalIsCapped);
    }

    // ---- Same rules as the ranked search ----

    [Fact]
    public void QuickSearch_FindsTheSameProductsAsTheRankedSearch()
    {
        var quick = _search.QuickSearch("wireless head").Hits.Select(h => h.ProductId).OrderBy(i => i);
        var ranked = _search.Search("wireless head").Hits.Select(h => h.ProductId).OrderBy(i => i);

        Assert.Equal(ranked, quick);
        Assert.Equal(2, quick.Count());
    }

    [Fact]
    public void QuickSearch_ReturnsBrandAndTierToo()
    {
        var hit = _search.QuickSearch("monitor").Hits.Single();

        Assert.Equal("Zenith", hit.BrandName);
        Assert.Equal(3, hit.QualityTier);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("!!!")]
    public void QuickSearch_EmptyOrSymbolOnlyText_ReturnsNothing(string? text)
    {
        var page = _search.QuickSearch(text);

        Assert.Empty(page.Hits);
        Assert.Equal(0, page.TotalMatches);
        Assert.False(page.TotalIsCapped);
    }

    [Theory]
    [InlineData("wireless AND")]
    [InlineData("NEAR(wireless headphones)")]
    [InlineData("name:wireless")]
    [InlineData("\"unclosed quote wireless")]
    [InlineData("wireless); DROP TABLE Products; --")]
    public void QuickSearch_SearchSyntaxTypedByTheUser_NeverThrows(string text)
    {
        Assert.Null(Record.Exception(() => _search.QuickSearch(text)));
        Assert.Equal(3, TestCatalog.Count(_db, "SELECT COUNT(*) FROM Products"));
    }

    [Fact]
    public void QuickSearch_LimitIsKeptWithinSensibleBounds()
    {
        AddGadgets(5);

        Assert.Single(_search.QuickSearch("gadget", limit: 0).Hits);
        Assert.Equal(5, _search.QuickSearch("gadget", limit: 100000).Hits.Count);
    }

    // ---- Async wrappers ----

    [Fact]
    public async Task SearchAsync_GivesTheSameResultAsTheSyncSearch()
    {
        var sync = _search.Search("wireless");
        var async = await _search.SearchAsync("wireless");

        Assert.Equal(sync.Hits, async.Hits);   // compared by content, not by list identity
        Assert.Equal(sync.TotalMatches, async.TotalMatches);
        Assert.Equal(sync.TotalIsCapped, async.TotalIsCapped);
    }

    [Fact]
    public async Task QuickSearchAsync_GivesTheSameResultAsTheSyncSearch()
    {
        var sync = _search.QuickSearch("headphones");
        var async = await _search.QuickSearchAsync("headphones");

        Assert.Equal(sync.Hits, async.Hits);
        Assert.Equal(sync.TotalMatches, async.TotalMatches);
        Assert.Equal(sync.TotalIsCapped, async.TotalIsCapped);
    }

    [Fact]
    public async Task SearchAsync_WithAnAlreadyCancelledToken_IsCancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _search.SearchAsync("wireless", cancellationToken: cts.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _search.QuickSearchAsync("wireless", cancellationToken: cts.Token));
    }

    // ---- Sharing the service between threads ----

    [Fact]
    public async Task ManySearchesAtOnce_AllGetTheSameCorrectAnswer()
    {
        AddGadgets(100);
        var expectedGadgets = _search.Search("gadget", 500).Hits.Select(h => h.ProductId).ToHashSet();
        var expectedQuick = _search.QuickSearch("wireless").Hits.Select(h => h.ProductId).ToList();

        var tasks = Enumerable.Range(0, 32).Select(i => Task.Run(() => i % 2 == 0
            ? (Kind: "ranked", Ids: _search.Search("gadget", 500).Hits.Select(h => h.ProductId).ToList())
            : (Kind: "quick", Ids: _search.QuickSearch("wireless").Hits.Select(h => h.ProductId).ToList()))).ToArray();
        var results = await Task.WhenAll(tasks);

        Assert.All(results.Where(r => r.Kind == "ranked"), r => Assert.True(expectedGadgets.SetEquals(r.Ids)));
        Assert.All(results.Where(r => r.Kind == "quick"), r => Assert.Equal(expectedQuick, r.Ids));
    }

    [Fact]
    public async Task SearchesKeepWorkingWhileProductsAreBeingAdded()
    {
        var writer = Task.Run(() =>
        {
            for (var i = 0; i < 40; i++)
                TestCatalog.AddProduct(_db, $"Concurrent Lamp {i}", "Lumo", 2, "lighting");
        });
        var readers = Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
        {
            for (var i = 0; i < 40; i++)
            {
                var page = _search.QuickSearch("lamp");
                Assert.True(page.Hits.Count <= page.TotalMatches); // never a half-written state
            }
        })).ToArray();

        await Task.WhenAll(readers.Append(writer));

        Assert.Equal(40, _search.Search("lamp").TotalMatches);
    }
}
