using Microsoft.Data.Sqlite;

namespace CatchIt.Core.Data;

public class Database
{
    private readonly string _path;
    private readonly string _connectionString;

    public Database(string dbPath)
    {
        _path = Path.GetFullPath(dbPath);

        // Pooling is off: a SQLite connection is cheap to open (tens of microseconds), and pooling
        // keeps connections and file handles alive in a process-wide cache. That cache can keep the
        // database file locked after the app has finished with it, and clearing it while other threads
        // are opening connections can fail. Without it, closing a connection really closes the file.
        _connectionString = $"Data Source={_path};Pooling=False";
    }

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CatchIt", "catchit.db");

    public SqliteConnection Open()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA foreign_keys = ON;";
        cmd.ExecuteNonQuery();
        return conn;
    }

    public void Initialize()
    {
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        using var conn = Open();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = Schema;
            cmd.ExecuteNonQuery();
        }

        // The price extremes table and the product search index (also added to existing databases).
        SearchAndPricingSchema.Ensure(conn);

        // Password-reset columns on Users (also added to databases created before they existed).
        EnsureColumn(conn, "Users", "ResetTokenExpires", "TEXT NULL");
        EnsureColumn(conn, "Users", "ResetAttempts", "INTEGER NOT NULL DEFAULT 0");
    }

    /// <summary>
    /// Adds a column to an existing table if it is not there yet. SQLite has no
    /// "ADD COLUMN IF NOT EXISTS", so the table's current columns are checked first.
    /// </summary>
    /// <param name="conn">An open connection.</param>
    /// <param name="table">Table name (a fixed name from this class, never user input).</param>
    /// <param name="column">Column to add.</param>
    /// <param name="definition">Type and constraints, e.g. <c>TEXT NULL</c>.</param>
    private static void EnsureColumn(SqliteConnection conn, string table, string column, string definition)
    {
        using (var check = conn.CreateCommand())
        {
            check.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name = $c";
            check.Parameters.AddWithValue("$c", column);
            if (Convert.ToInt64(check.ExecuteScalar()) > 0)
                return;
        }

        using var add = conn.CreateCommand();
        add.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition}";
        add.ExecuteNonQuery();
    }

    private const string Schema = """
        CREATE TABLE IF NOT EXISTS Users (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Name TEXT NOT NULL DEFAULT '',
            Email TEXT NOT NULL UNIQUE COLLATE NOCASE,
            PasswordHash TEXT NOT NULL,
            IsConfirmed INTEGER NOT NULL DEFAULT 0,
            ResetToken TEXT NULL
        );

        CREATE TABLE IF NOT EXISTS Marketplaces (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Name TEXT NOT NULL UNIQUE
        );

        -- QualityTier: 1 = Budget, 2 = Standard, 3 = Premium
        CREATE TABLE IF NOT EXISTS Brands (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Name TEXT NOT NULL UNIQUE,
            QualityTier INTEGER NOT NULL CHECK (QualityTier BETWEEN 1 AND 3)
        );

        CREATE TABLE IF NOT EXISTS Products (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Name TEXT NOT NULL UNIQUE,
            Keywords TEXT NOT NULL DEFAULT '',
            BrandId INTEGER NOT NULL REFERENCES Brands(Id),
            Category TEXT NOT NULL DEFAULT '',
            TypicalPrice REAL NULL,
            AverageRating REAL NULL,
            RatingCount INTEGER NULL
        );

        -- One row per product per marketplace. CurrentPrice is NULL when unavailable.
        CREATE TABLE IF NOT EXISTS Listings (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            ProductId INTEGER NOT NULL REFERENCES Products(Id),
            MarketplaceId INTEGER NOT NULL REFERENCES Marketplaces(Id),
            SellerRating REAL NOT NULL CHECK (SellerRating BETWEEN 0 AND 5),
            CurrentPrice REAL NULL CHECK (CurrentPrice IS NULL OR CurrentPrice > 0),
            UNIQUE (ProductId, MarketplaceId)
        );

        -- The PriceHistory table (compact weekly history) is created in SearchAndPricingSchema,
        -- which also upgrades databases that still have the older, larger layout.

        -- Manually curated "cheaper alternative" mapping (no automatic similarity).
        CREATE TABLE IF NOT EXISTS Alternatives (
            ProductId INTEGER NOT NULL REFERENCES Products(Id),
            AlternativeProductId INTEGER NOT NULL REFERENCES Products(Id),
            PRIMARY KEY (ProductId, AlternativeProductId)
        );

        CREATE TABLE IF NOT EXISTS Watchlist (
            UserId INTEGER NOT NULL REFERENCES Users(Id),
            ProductId INTEGER NOT NULL REFERENCES Products(Id),
            TargetPrice REAL NULL CHECK (TargetPrice IS NULL OR TargetPrice > 0),
            PRIMARY KEY (UserId, ProductId)
        );

        CREATE TABLE IF NOT EXISTS RefreshLog (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            RefreshedAt TEXT NOT NULL,
            ListingsUpdated INTEGER NOT NULL
        );
        """;
}
