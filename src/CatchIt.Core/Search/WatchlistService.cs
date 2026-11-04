using CatchIt.Core.Auth;
using CatchIt.Core.Data;

namespace CatchIt.Core.Search;

/// <summary>Outcome of a watchlist action. <see cref="Message"/> is always the text to show the user.</summary>
/// <param name="Success">True when the action was carried out.</param>
/// <param name="Message">What to tell the user, on success or failure.</param>
public record WatchlistResult(bool Success, string Message);

/// <summary>One product on a user's watchlist.</summary>
/// <param name="ProductId">The product.</param>
/// <param name="ProductName">Its name.</param>
/// <param name="BrandName">Its brand.</param>
/// <param name="CurrentPrice">The lowest current price across its marketplaces, or <c>null</c> when none has a price. SIMULATED.</param>
/// <param name="MarketplaceName">The marketplace offering <paramref name="CurrentPrice"/>, or <c>null</c> when there is none.</param>
/// <param name="TargetPrice">The price the user wants, or <c>null</c> when none is set.</param>
/// <param name="ListingId">The cheapest priced listing (to open its detail window), or <c>null</c> when none has a price.</param>
public record WatchedProduct(
    long ProductId, string ProductName, string BrandName, double? CurrentPrice, string? MarketplaceName,
    double? TargetPrice, long? ListingId)
{
    /// <summary>True when a target is set and the current price has reached it (is at or below it).</summary>
    public bool IsBelowTarget => CurrentPrice is { } p && TargetPrice is { } t && p <= t;
}

/// <summary>
/// A logged-in user's watchlist (user stories #13 and #14): add and remove products, set a target price,
/// and see each product's lowest current price. Guests cannot save, so every action refuses them.
/// </summary>
/// <remarks>
/// The watchlist follows the product, not one marketplace's listing: its price is the lowest current price
/// of any of its listings. See <see cref="Access"/> for who may use it.
/// </remarks>
public class WatchlistService
{
    public const string AddedMessage = "Added to your watchlist";
    public const string AlreadyWatchedMessage = "This product is already on your watchlist";
    public const string RemovedMessage = "Removed from your watchlist";
    public const string NotWatchedMessage = "This product is not on your watchlist";
    public const string NoSuchProductMessage = "This product is no longer available";
    public const string TargetSavedMessage = "Target price saved";
    public const string TargetClearedMessage = "Target price removed";
    public const string InvalidTargetMessage = "Target price must be greater than 0";

    private readonly Database _db;
    private readonly Session _session;

    /// <summary>Creates the service.</summary>
    /// <param name="db">Where the watchlist is saved.</param>
    /// <param name="session">Identifies whose watchlist it is.</param>
    public WatchlistService(Database db, Session session)
    {
        _db = db;
        _session = session;
    }

    /// <summary>The access failure for the current session, or <c>null</c> when it may use the watchlist.</summary>
    private WatchlistResult? Denied(out long userId)
    {
        userId = _session.CurrentUser?.Id ?? 0;
        var access = Access.Check(_session, Feature.Watchlist);
        return access.Allowed ? null : new WatchlistResult(false, access.Reason!);
    }

    /// <summary>Adds a product to the watchlist.</summary>
    /// <param name="productId">The product to watch.</param>
    /// <returns>Success, or the reason it was refused (guest, unknown product, already watched).</returns>
    public WatchlistResult Add(long productId)
    {
        if (Denied(out var userId) is { } denied) return denied;

        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        // Checking in the same statement keeps an unknown product from reaching the foreign key.
        cmd.CommandText = "INSERT OR IGNORE INTO Watchlist (UserId, ProductId) SELECT $u, Id FROM Products WHERE Id = $p";
        cmd.Parameters.AddWithValue("$u", userId);
        cmd.Parameters.AddWithValue("$p", productId);
        if (cmd.ExecuteNonQuery() == 1) return new WatchlistResult(true, AddedMessage);

        // Nothing inserted: either the product is missing or it was already there.
        return IsWatching(productId)
            ? new WatchlistResult(false, AlreadyWatchedMessage)
            : new WatchlistResult(false, NoSuchProductMessage);
    }

    /// <summary>Removes a product (and its target price) from the watchlist.</summary>
    /// <param name="productId">The product to stop watching.</param>
    public WatchlistResult Remove(long productId)
    {
        if (Denied(out var userId) is { } denied) return denied;

        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Watchlist WHERE UserId = $u AND ProductId = $p";
        cmd.Parameters.AddWithValue("$u", userId);
        cmd.Parameters.AddWithValue("$p", productId);
        return cmd.ExecuteNonQuery() == 1
            ? new WatchlistResult(true, RemovedMessage)
            : new WatchlistResult(false, NotWatchedMessage);
    }

    /// <summary>Sets or clears the target price of a watched product.</summary>
    /// <param name="productId">The watched product.</param>
    /// <param name="targetPrice">The wanted price in dollars, which must be above 0; <c>null</c> clears it.</param>
    public WatchlistResult SetTargetPrice(long productId, double? targetPrice)
    {
        if (Denied(out var userId) is { } denied) return denied;
        // NaN and infinity fail the comparison too, so only real positive numbers get through.
        if (targetPrice is { } t && !(t > 0 && double.IsFinite(t)))
            return new WatchlistResult(false, InvalidTargetMessage);

        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE Watchlist SET TargetPrice = $t WHERE UserId = $u AND ProductId = $p";
        cmd.Parameters.AddWithValue("$t", (object?)targetPrice ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$u", userId);
        cmd.Parameters.AddWithValue("$p", productId);
        if (cmd.ExecuteNonQuery() != 1) return new WatchlistResult(false, NotWatchedMessage);
        return new WatchlistResult(true, targetPrice == null ? TargetClearedMessage : TargetSavedMessage);
    }

    /// <summary>Whether the logged-in user is watching a product. Always false for a guest.</summary>
    /// <param name="productId">The product to check.</param>
    public bool IsWatching(long productId)
    {
        if (_session.CurrentUser is not { } user) return false;
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM Watchlist WHERE UserId = $u AND ProductId = $p";
        cmd.Parameters.AddWithValue("$u", user.Id);
        cmd.Parameters.AddWithValue("$p", productId);
        return cmd.ExecuteScalar() != null;
    }

    /// <summary>The logged-in user's watched products, A to Z. Empty for a guest.</summary>
    public IReadOnlyList<WatchedProduct> List()
    {
        var items = new List<WatchedProduct>();
        if (_session.CurrentUser is not { } user) return items;

        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        // The cheapest priced listing of each product; ties go to the lower listing id so the result is stable.
        cmd.CommandText = """
            SELECT p.Id, p.Name, b.Name, w.TargetPrice, best.Id, best.CurrentPrice, m.Name
            FROM Watchlist w
            JOIN Products p ON p.Id = w.ProductId
            JOIN Brands b ON b.Id = p.BrandId
            LEFT JOIN Listings best ON best.Id = (
                SELECT x.Id FROM Listings x WHERE x.ProductId = p.Id AND x.CurrentPrice IS NOT NULL
                ORDER BY x.CurrentPrice, x.Id LIMIT 1)
            LEFT JOIN Marketplaces m ON m.Id = best.MarketplaceId
            WHERE w.UserId = $u
            ORDER BY p.Name COLLATE NOCASE, p.Id
            """;
        cmd.Parameters.AddWithValue("$u", user.Id);
        using var r = cmd.ExecuteReader();
        while (r.Read())
            items.Add(new WatchedProduct(
                r.GetInt64(0), r.GetString(1), r.GetString(2),
                r.IsDBNull(5) ? null : r.GetDouble(5), r.IsDBNull(6) ? null : r.GetString(6),
                r.IsDBNull(3) ? null : r.GetDouble(3), r.IsDBNull(4) ? null : r.GetInt64(4)));
        return items;
    }
}
