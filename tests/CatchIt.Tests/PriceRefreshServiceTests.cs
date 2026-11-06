using CatchIt.Core.Auth;
using CatchIt.Core.Data;
using CatchIt.Core.Pricing;

namespace CatchIt.Tests;

/// <summary>
/// Tests for the admin-only price refresh (issue #19): who may run it, what it changes,
/// and that it keeps the price history in its compact shape. Each test gets its own database.
/// </summary>
/// <remarks>
/// The new prices are random, so the tests check the rules every price must follow (moved by at
/// most <see cref="PriceRefreshService.MaxChangePercent"/>%, whole cents) rather than exact values.
/// </remarks>
public class PriceRefreshServiceTests : TempDatabaseTests
{
    private static readonly DateTime Now = new(2026, 11, 5, 18, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = DateOnly.FromDateTime(Now);

    private readonly Session _session = new();
    private readonly UserService _users;
    private readonly PriceRefreshService _refresh;

    public PriceRefreshServiceTests()
    {
        _users = new UserService(Db, _session);
        _refresh = new PriceRefreshService(Db, _session, () => Now);
    }

    private void LogInAsAdmin()
    {
        new Seeder(Db).Run();
        Assert.True(_users.Login(Seeder.AdminEmail, Seeder.AdminPassword).Success);
    }

    private double? Price(long listingId)
    {
        using var conn = Db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT CurrentPrice FROM Listings WHERE Id = {listingId}";
        var value = cmd.ExecuteScalar();
        return value is DBNull or null ? null : Convert.ToDouble(value);
    }

    private Dictionary<long, double> AllPrices()
    {
        using var conn = Db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, CurrentPrice FROM Listings WHERE CurrentPrice IS NOT NULL";
        using var reader = cmd.ExecuteReader();
        var prices = new Dictionary<long, double>();
        while (reader.Read()) prices[reader.GetInt64(0)] = reader.GetDouble(1);
        return prices;
    }

    private long Count(string sql) => TestCatalog.Count(Db, sql);

    // ---- Who can refresh ----

    [Fact]
    public void Refresh_AsGuest_IsRefused_AndChangesNothing()
    {
        new Seeder(Db).Run();
        var before = AllPrices();
        _session.EnterGuestMode();

        var result = _refresh.Refresh();

        Assert.False(result.Success);
        Assert.Equal(Access.AdminOnlyMessage, result.Error);
        Assert.Equal(before, AllPrices());
        Assert.Equal(0, Count("SELECT COUNT(*) FROM RefreshLog"));
    }

    [Fact]
    public void Refresh_AsRegularUser_IsRefused()
    {
        new Seeder(Db).Run();
        _users.Login("demo@catchit.test", "Demo1234!");

        var result = _refresh.Refresh();

        Assert.False(result.Success);
        Assert.Equal(Access.AdminOnlyMessage, result.Error);
        Assert.Equal(0, Count("SELECT COUNT(*) FROM RefreshLog"));
    }

    [Fact]
    public void Refresh_SignedOut_AsksToLogIn()
    {
        var result = _refresh.Refresh();

        Assert.False(result.Success);
        Assert.Equal(Access.LoginOrGuestMessage, result.Error);
    }

    [Fact]
    public void AdminLogin_MarksSessionAsAdmin_RegularLoginDoesNot()
    {
        new Seeder(Db).Run();

        _users.Login("demo@catchit.test", "Demo1234!");
        Assert.False(_session.IsAdmin);

        _users.Login(Seeder.AdminEmail, Seeder.AdminPassword);
        Assert.True(_session.IsAdmin);
        Assert.True(Access.CanUse(_session, Feature.PriceRefresh));
    }

    [Fact]
    public void NewAccounts_AreNotAdmins()
    {
        _users.Register("shopper@example.com", "Passw0rd1");
        _users.Login("shopper@example.com", "Passw0rd1");

        Assert.False(_session.IsAdmin);
        Assert.False(Access.CanUse(_session, Feature.PriceRefresh));
    }

    // ---- What a refresh changes ----

    [Fact]
    public void Refresh_AsAdmin_UpdatesEveryAvailableListing_WithinTheLimit()
    {
        LogInAsAdmin();
        var before = AllPrices();

        var result = _refresh.Refresh();

        Assert.True(result.Success);
        Assert.Equal(before.Count, result.ListingsUpdated);
        Assert.Equal(Now, result.RefreshedAt);

        var after = AllPrices();
        Assert.Equal(before.Keys.OrderBy(k => k), after.Keys.OrderBy(k => k));
        foreach (var (id, oldPrice) in before)
        {
            var newPrice = after[id];
            var limit = oldPrice * PriceRefreshService.MaxChangePercent / 100.0 + 0.005; // + half a cent of rounding
            Assert.InRange(newPrice, oldPrice - limit, oldPrice + limit);
            Assert.Equal(Math.Round(newPrice, 2), newPrice, 9); // whole cents
            Assert.True(newPrice > 0);
        }
    }

    [Fact]
    public void Refresh_LeavesUnavailableListingsAlone()
    {
        LogInAsAdmin();
        var product = TestCatalog.AddProduct(Db, "Out Of Stock Gadget");
        var unavailable = TestCatalog.AddListing(Db, product, currentPrice: null);

        _refresh.Refresh();

        Assert.Null(Price(unavailable));
        Assert.Equal(0, Count($"SELECT COUNT(*) FROM PriceHistory WHERE ListingId = {unavailable}"));
    }

    [Fact]
    public void Refresh_IsLogged_AndLastRefreshReportsIt()
    {
        LogInAsAdmin();
        Assert.Null(_refresh.LastRefresh());

        var result = _refresh.Refresh();

        var last = _refresh.LastRefresh();
        Assert.NotNull(last);
        Assert.Equal(Now, last!.RefreshedAt);
        Assert.Equal(result.ListingsUpdated, last.ListingsUpdated);
        Assert.Equal(1, Count("SELECT COUNT(*) FROM RefreshLog"));
    }

    // ---- History stays compact and correct ----

    [Fact]
    public void Refresh_ListingWithNoHistory_GetsAPointAndExtremesForToday()
    {
        LogInAsAdmin();
        var listing = TestCatalog.AddListing(Db, TestCatalog.AddProduct(Db, "Brand New Gadget"), currentPrice: 40);

        _refresh.Refresh();

        var price = Price(listing)!.Value;
        var history = new PriceHistoryService(Db);
        var extremes = history.GetExtremes(listing);
        Assert.NotNull(extremes);
        Assert.Equal(price, extremes!.LowPrice);
        Assert.Equal(price, extremes.HighPrice);
        Assert.Equal(Today, extremes.LowDate);
        Assert.Equal(1, Count($"SELECT COUNT(*) FROM PriceHistory WHERE ListingId = {listing} AND Date = '{Today:yyyy-MM-dd}'"));
    }

    [Fact]
    public void Refresh_RecentPointAndNoNewExtreme_AddsNoHistoryPoint()
    {
        LogInAsAdmin();
        var listing = TestCatalog.AddListing(Db, TestCatalog.AddProduct(Db, "Steady Gadget"), currentPrice: 50);
        // A point two days ago, and extremes far outside the ±5% a refresh can reach.
        TestCatalog.AddPoint(Db, listing, Today.AddDays(-2), 50);
        Sql("INSERT INTO PriceExtremes (ListingId, LowPrice, LowDate, HighPrice, HighDate) VALUES ($l, 10, '2026-01-01', 100, '2026-02-01')",
            ("$l", listing));

        _refresh.Refresh();

        Assert.Equal(1, Count($"SELECT COUNT(*) FROM PriceHistory WHERE ListingId = {listing}"));
        var extremes = new PriceHistoryService(Db).GetExtremes(listing)!;
        Assert.Equal(10, extremes.LowPrice);
        Assert.Equal(100, extremes.HighPrice);
    }

    [Fact]
    public void Refresh_NoPointForOverAWeek_AddsOne()
    {
        LogInAsAdmin();
        var listing = TestCatalog.AddListing(Db, TestCatalog.AddProduct(Db, "Quiet Gadget"), currentPrice: 50);
        TestCatalog.AddPoint(Db, listing, Today.AddDays(-10), 50);
        Sql("INSERT INTO PriceExtremes (ListingId, LowPrice, LowDate, HighPrice, HighDate) VALUES ($l, 10, '2026-01-01', 100, '2026-02-01')",
            ("$l", listing));

        _refresh.Refresh();

        Assert.Equal(2, Count($"SELECT COUNT(*) FROM PriceHistory WHERE ListingId = {listing}"));
    }

    [Fact]
    public void Refresh_PriceBelowStoredLow_BecomesTheNewLow()
    {
        LogInAsAdmin();
        var listing = TestCatalog.AddListing(Db, TestCatalog.AddProduct(Db, "Cheap Gadget"), currentPrice: 50);
        TestCatalog.AddPoint(Db, listing, Today.AddDays(-1), 50);
        // The stored low is above anything a refresh of $50 can produce, so the new price must beat it.
        Sql("INSERT INTO PriceExtremes (ListingId, LowPrice, LowDate, HighPrice, HighDate) VALUES ($l, 60, '2026-01-01', 100, '2026-02-01')",
            ("$l", listing));

        _refresh.Refresh();

        var extremes = new PriceHistoryService(Db).GetExtremes(listing)!;
        Assert.Equal(Price(listing), extremes.LowPrice);
        Assert.Equal(Today, extremes.LowDate);
        Assert.Equal(100, extremes.HighPrice);
        // A new low is always stored, even though there was a point yesterday.
        Assert.Equal(1, Count($"SELECT COUNT(*) FROM PriceHistory WHERE ListingId = {listing} AND Date = '{Today:yyyy-MM-dd}'"));
    }

    [Fact]
    public void Refresh_TwiceInADay_KeepsOnePointForToday_WithTheLatestPrice()
    {
        LogInAsAdmin();
        var listing = TestCatalog.AddListing(Db, TestCatalog.AddProduct(Db, "Busy Gadget"), currentPrice: 40);

        _refresh.Refresh();
        _refresh.Refresh();

        using var conn = Db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT Price FROM PriceHistory WHERE ListingId = {listing} AND Date = '{Today:yyyy-MM-dd}'";
        Assert.Equal(Price(listing), Convert.ToDouble(cmd.ExecuteScalar()));
        Assert.Equal(1, Count($"SELECT COUNT(*) FROM PriceHistory WHERE ListingId = {listing} AND Date = '{Today:yyyy-MM-dd}'"));
        Assert.Equal(2, Count("SELECT COUNT(*) FROM RefreshLog"));
    }

    // ---- Scenario 3: no seeded data ----

    /// <summary>An administrator in an otherwise empty database (no seeding, so no products).</summary>
    private void LogInAsAdminWithEmptyCatalog()
    {
        _users.Register("admin@example.com", "Passw0rd1");
        Sql("UPDATE Users SET IsAdmin = 1, IsConfirmed = 1 WHERE Email = 'admin@example.com'");
        Assert.True(_users.Login("admin@example.com", "Passw0rd1").Success);
        Assert.True(_session.IsAdmin);
    }

    [Fact]
    public void Refresh_NoSeededProducts_ReportsNothingToUpdate_WithoutError()
    {
        LogInAsAdminWithEmptyCatalog();

        var result = _refresh.Refresh();

        Assert.True(result.Success);
        Assert.True(result.NothingToUpdate);
        Assert.Equal(0, result.ListingsUpdated);
        Assert.Equal(PriceRefreshService.NothingToUpdateMessage, result.Describe());
        Assert.Equal(0, Count("SELECT COUNT(*) FROM RefreshLog"));
    }

    [Fact]
    public void Refresh_OnlyUnavailableListings_ReportsNothingToUpdate()
    {
        LogInAsAdminWithEmptyCatalog();
        TestCatalog.AddListing(Db, TestCatalog.AddProduct(Db, "Sold Out Gadget"), currentPrice: null);

        var result = _refresh.Refresh();

        Assert.True(result.NothingToUpdate);
        Assert.Equal(0, Count("SELECT COUNT(*) FROM PriceHistory"));
    }

    // ---- What the page and the command line show ----

    [Fact]
    public void Describe_Success_SaysHowManyListingsChanged()
    {
        LogInAsAdmin();

        var result = _refresh.Refresh();

        Assert.False(result.NothingToUpdate);
        Assert.StartsWith($"Updated {result.ListingsUpdated:N0} listings at ", result.Describe());
    }

    [Fact]
    public void Describe_Refused_ShowsTheReason()
    {
        new Seeder(Db).Run();
        _users.Login("demo@catchit.test", "Demo1234!");

        Assert.Equal(Access.AdminOnlyMessage, _refresh.Refresh().Describe());
    }

    // ---- Existing databases ----

    [Fact]
    public void OlderDatabaseWithoutIsAdminColumn_IsUpgradedOnOpen()
    {
        Sql("ALTER TABLE Users DROP COLUMN IsAdmin");

        Db.Initialize();

        Assert.Equal(1, Count("SELECT COUNT(*) FROM pragma_table_info('Users') WHERE name = 'IsAdmin'"));
    }
}
