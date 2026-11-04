using CatchIt.Core.Auth;
using CatchIt.Core.Data;
using CatchIt.Core.Search;
using Microsoft.Data.Sqlite;

namespace CatchIt.Tests;

/// <summary>Tests for <see cref="WatchlistService"/> (user stories #13 and #14).</summary>
public class WatchlistServiceTests : TempDatabaseTests
{
    private const string Password = "Passw0rd1";

    private readonly Session _session = new();
    private readonly UserService _users;
    private readonly WatchlistService _watch;

    public WatchlistServiceTests()
    {
        _users = new UserService(Db, _session);
        _watch = new WatchlistService(Db, _session);
    }

    private void LoginAs(string email)
    {
        _users.Register(email, Password, "Shopper");
        _users.Login(email, Password);
    }

    [Fact]
    public void Add_PutsTheProductOnTheList_AndRefusesADuplicate()
    {
        LoginAs("a@example.com");
        var p = TestCatalog.AddProduct(Db, "Widget");

        Assert.Equal(new WatchlistResult(true, WatchlistService.AddedMessage), _watch.Add(p));
        Assert.True(_watch.IsWatching(p));
        Assert.Equal(new WatchlistResult(false, WatchlistService.AlreadyWatchedMessage), _watch.Add(p));
        Assert.Single(_watch.List());
    }

    [Fact]
    public void Add_UnknownProduct_IsRefused()
    {
        LoginAs("a@example.com");
        Assert.Equal(new WatchlistResult(false, WatchlistService.NoSuchProductMessage), _watch.Add(9999));
        Assert.Empty(_watch.List());
    }

    [Fact]
    public void Guests_AndSignedOutUsers_CannotUseIt()
    {
        var p = TestCatalog.AddProduct(Db, "Widget");

        Assert.Equal(Access.LoginOrGuestMessage, _watch.Add(p).Message); // signed out
        _session.EnterGuestMode();
        Assert.Equal(new WatchlistResult(false, Access.LoginRequiredMessage), _watch.Add(p));
        Assert.False(_watch.Remove(p).Success);
        Assert.False(_watch.SetTargetPrice(p, 10).Success);
        Assert.False(_watch.IsWatching(p));
        Assert.Empty(_watch.List());
    }

    [Fact]
    public void Remove_TakesItOff_AndAskingAgainSaysItIsNotThere()
    {
        LoginAs("a@example.com");
        var p = TestCatalog.AddProduct(Db, "Widget");
        _watch.Add(p);

        Assert.Equal(new WatchlistResult(true, WatchlistService.RemovedMessage), _watch.Remove(p));
        Assert.False(_watch.IsWatching(p));
        Assert.Equal(new WatchlistResult(false, WatchlistService.NotWatchedMessage), _watch.Remove(p));
    }

    [Fact]
    public void TargetPrice_CanBeSetChangedAndCleared()
    {
        LoginAs("a@example.com");
        var p = TestCatalog.AddProduct(Db, "Widget");
        _watch.Add(p);

        Assert.Equal(WatchlistService.TargetSavedMessage, _watch.SetTargetPrice(p, 25.5).Message);
        Assert.Equal(25.5, _watch.List()[0].TargetPrice);
        _watch.SetTargetPrice(p, 20);
        Assert.Equal(20, _watch.List()[0].TargetPrice);
        Assert.Equal(WatchlistService.TargetClearedMessage, _watch.SetTargetPrice(p, null).Message);
        Assert.Null(_watch.List()[0].TargetPrice);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void TargetPrice_MustBeAboveZero(double bad)
    {
        LoginAs("a@example.com");
        var p = TestCatalog.AddProduct(Db, "Widget");
        _watch.Add(p);
        _watch.SetTargetPrice(p, 30);

        Assert.Equal(new WatchlistResult(false, WatchlistService.InvalidTargetMessage), _watch.SetTargetPrice(p, bad));
        Assert.Equal(30, _watch.List()[0].TargetPrice); // unchanged
    }

    [Fact]
    public void TargetPrice_OnAProductNotWatched_IsRefused()
    {
        LoginAs("a@example.com");
        var p = TestCatalog.AddProduct(Db, "Widget");
        Assert.Equal(new WatchlistResult(false, WatchlistService.NotWatchedMessage), _watch.SetTargetPrice(p, 10));
    }

    [Fact]
    public void List_ShowsTheCheapestPricedListing_AndIgnoresUnpricedOnes()
    {
        LoginAs("a@example.com");
        var p = TestCatalog.AddProduct(Db, "Widget");
        TestCatalog.AddListing(Db, p, "Dear", 40);
        var cheap = TestCatalog.AddListing(Db, p, "Cheap", 30);
        TestCatalog.AddListing(Db, p, "NoPrice", null);
        _watch.Add(p);

        var item = Assert.Single(_watch.List());
        Assert.Equal(30, item.CurrentPrice);
        Assert.Equal("Cheap", item.MarketplaceName);
        Assert.Equal(cheap, item.ListingId);
    }

    [Fact]
    public void List_ProductWithNoPrice_HasNoPriceAndIsNeverBelowTarget()
    {
        LoginAs("a@example.com");
        var p = TestCatalog.AddProduct(Db, "Widget");
        TestCatalog.AddListing(Db, p, "Shop", null);
        _watch.Add(p);
        _watch.SetTargetPrice(p, 10);

        var item = Assert.Single(_watch.List());
        Assert.Null(item.CurrentPrice);
        Assert.Null(item.ListingId);
        Assert.False(item.IsBelowTarget);
    }

    [Theory]
    [InlineData(30, 30, true)]   // reaching the target counts
    [InlineData(29.99, 30, true)]
    [InlineData(30.01, 30, false)]
    public void IsBelowTarget_MeansAtOrUnderTheTarget(double price, double target, bool expected)
    {
        Assert.Equal(expected, new WatchedProduct(1, "W", "B", price, "M", target, 1).IsBelowTarget);
        Assert.False(new WatchedProduct(1, "W", "B", price, "M", null, 1).IsBelowTarget);
    }

    [Fact]
    public void Lists_AreSortedByName_AndEachUserSeesOnlyTheirOwn()
    {
        LoginAs("a@example.com");
        var b = TestCatalog.AddProduct(Db, "banana");
        var a = TestCatalog.AddProduct(Db, "Apple");
        _watch.Add(b);
        _watch.Add(a);
        Assert.Equal(new[] { "Apple", "banana" }, _watch.List().Select(i => i.ProductName));

        _users.Logout();
        LoginAs("b@example.com");
        Assert.Empty(_watch.List());
        Assert.False(_watch.IsWatching(a));
        Assert.Equal(WatchlistService.AddedMessage, _watch.Add(a).Message); // the other user's entry does not block this one
    }

    [Fact]
    public void CheapestListingLookup_UsesAnIndex()
    {
        // The cheapest-listing lookup must use an index, not walk every listing of a 1.5M-row table.
        LoginAs("a@example.com");
        var p = TestCatalog.AddProduct(Db, "Widget");
        TestCatalog.AddListing(Db, p, "Shop", 12);
        _watch.Add(p);
        using var conn = Db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "EXPLAIN QUERY PLAN SELECT x.Id FROM Listings x WHERE x.ProductId = 1 AND x.CurrentPrice IS NOT NULL ORDER BY x.CurrentPrice, x.Id LIMIT 1";
        using var r = cmd.ExecuteReader();
        var plan = string.Join(" | ", Enumerable.Range(0, 1).SelectMany(_ => ReadPlan(r)));
        Assert.DoesNotContain("SCAN x", plan);
    }

    private static IEnumerable<string> ReadPlan(SqliteDataReader r)
    {
        while (r.Read()) yield return r.GetString(3);
    }
}
