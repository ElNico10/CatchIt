using CatchIt.Core.Auth;
using CatchIt.Core.Search;

namespace CatchIt.Tests;

/// <summary>Tests for <see cref="DashboardService"/> (user story #15): which watched products are at or under their target.</summary>
public class DashboardServiceTests : TempDatabaseTests
{
    private static readonly DateOnly Today = new(2026, 11, 3);

    private readonly Session _session = new();
    private readonly WatchlistService _watch;
    private readonly DashboardService _dashboard;

    public DashboardServiceTests()
    {
        var users = new UserService(Db, _session);
        users.Register("shopper@example.com", "Passw0rd1", "Shopper");
        users.Login("shopper@example.com", "Passw0rd1");
        _watch = new WatchlistService(Db, _session);
        _dashboard = new DashboardService(_watch, Db, Today);
    }

    /// <summary>Watches a new product with one listing at <paramref name="price"/> and the given target.</summary>
    private (long Product, long Listing) Watch(string name, double? price, double? target)
    {
        var product = TestCatalog.AddProduct(Db, name);
        var listing = TestCatalog.AddListing(Db, product, "Shop", price);
        _watch.Add(product);
        if (target != null) _watch.SetTargetPrice(product, target);
        return (product, listing);
    }

    private static string[] Names(IEnumerable<DashboardEntry> entries) => entries.Select(e => e.Item.ProductName).ToArray();

    // ---- The user story's scenarios ----

    [Fact]
    public void AProductUnderItsTarget_AppearsInBelowTarget()
    {
        Watch("Widget", price: 45, target: 50);

        var view = _dashboard.Load();

        Assert.Equal(new[] { "Widget" }, Names(view.BelowTarget));
        Assert.Empty(view.NotYetAtTarget);
    }

    [Fact]
    public void AProductAboveItsTarget_DoesNotAppearInBelowTarget()
    {
        Watch("Widget", price: 55, target: 50);

        var view = _dashboard.Load();

        Assert.Empty(view.BelowTarget);
        Assert.Equal(new[] { "Widget" }, Names(view.NotYetAtTarget));
    }

    [Fact]
    public void ReachingTheTargetExactly_Counts()
    {
        Watch("Widget", price: 50, target: 50);

        Assert.Single(_dashboard.Load().BelowTarget);
    }

    // ---- Gaps ----

    [Fact]
    public void Gaps_AreMeasuredAgainstTheTarget()
    {
        Watch("Under", price: 45, target: 50);
        Watch("Over", price: 55, target: 50);

        var view = _dashboard.Load();

        Assert.Equal(-5, view.BelowTarget[0].GapToTarget!.Value, 6);
        Assert.Equal(-0.10, view.BelowTarget[0].GapToTargetFraction!.Value, 6);
        Assert.Equal(5, view.NotYetAtTarget[0].GapToTarget!.Value, 6);
        Assert.Equal(0.10, view.NotYetAtTarget[0].GapToTargetFraction!.Value, 6);
    }

    [Fact]
    public void TheAverageComparison_ShowsWhetherTheTargetIsRealistic()
    {
        var (_, listing) = Watch("Widget", price: 90, target: 40);
        TestCatalog.AddPoint(Db, listing, Today.AddDays(-10), 100);
        TestCatalog.AddPoint(Db, listing, Today.AddDays(-30), 100);

        var entry = _dashboard.Load().NotYetAtTarget.Single();

        Assert.Equal(100, entry.Average90);
        Assert.Equal(-0.10, entry.VersusAverageFraction!.Value, 6); // 10% under its usual price, yet the target is 56% lower
    }

    [Fact]
    public void WithoutEnoughHistory_ThereIsNoAverageComparison()
    {
        var (_, listing) = Watch("Widget", price: 90, target: 40);
        TestCatalog.AddPoint(Db, listing, Today.AddDays(-10), 100); // one point is not an average

        var entry = _dashboard.Load().NotYetAtTarget.Single();

        Assert.Null(entry.Average90);
        Assert.Null(entry.VersusAverageFraction);
    }

    // ---- Which list ----

    [Fact]
    public void ProductsWithoutATarget_AreCountedButNotListed()
    {
        Watch("No target", price: 10, target: null);
        Watch("Has target", price: 10, target: 20);

        var view = _dashboard.Load();

        Assert.Equal(1, view.WithoutTarget);
        Assert.Equal(new[] { "Has target" }, Names(view.BelowTarget));
        Assert.Empty(view.NotYetAtTarget);
    }

    [Fact]
    public void AProductWithNoPrice_IsNeverBelowTarget_AndComesLast()
    {
        Watch("Unpriced", price: null, target: 10);
        Watch("Far", price: 100, target: 10);
        Watch("Near", price: 11, target: 10);

        var view = _dashboard.Load();

        Assert.Empty(view.BelowTarget);
        Assert.Equal(new[] { "Near", "Far", "Unpriced" }, Names(view.NotYetAtTarget));
        Assert.Null(view.NotYetAtTarget[2].GapToTarget);
    }

    [Fact]
    public void BelowTarget_ListsTheBiggestDropFirst()
    {
        Watch("Slightly", price: 49, target: 50);
        Watch("Deeply", price: 25, target: 50);
        Watch("Middle", price: 40, target: 50);

        Assert.Equal(new[] { "Deeply", "Middle", "Slightly" }, Names(_dashboard.Load().BelowTarget));
    }

    [Fact]
    public void TheCheapestListingDecidesThePrice()
    {
        var (product, _) = Watch("Widget", price: 60, target: 50);
        TestCatalog.AddListing(Db, product, "Cheaper", 45);

        var entry = _dashboard.Load().BelowTarget.Single();

        Assert.Equal(45, entry.Item.CurrentPrice);
    }

    // ---- Who ----

    [Fact]
    public void AGuest_GetsAnEmptyDashboard()
    {
        Watch("Widget", price: 45, target: 50);
        _session.EnterGuestMode();

        var view = _dashboard.Load();

        Assert.Empty(view.BelowTarget);
        Assert.Empty(view.NotYetAtTarget);
        Assert.Equal(0, view.WithoutTarget);
    }

    [Fact]
    public void EachUser_SeesOnlyTheirOwnProducts()
    {
        Watch("Mine", price: 45, target: 50);
        _session.SignOut();
        var users = new UserService(Db, _session);
        users.Register("other@example.com", "Passw0rd1", "Other");
        users.Login("other@example.com", "Passw0rd1");

        Assert.Empty(_dashboard.Load().BelowTarget);
    }
}
