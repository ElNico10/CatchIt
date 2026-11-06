using Microsoft.Data.Sqlite;

namespace CatchIt.Core.Data;

/// <summary>
/// Adds the <c>Users.IsAdmin</c> column (issue #19) to databases created before it existed.
/// New databases already have it from the main schema.
/// </summary>
internal static class AdminSchema
{
    /// <summary>Adds the column if it is missing. Safe to call every time the database is opened.</summary>
    /// <param name="conn">An open connection.</param>
    public static void Ensure(SqliteConnection conn)
    {
        using (var check = conn.CreateCommand())
        {
            check.CommandText = "SELECT COUNT(*) FROM pragma_table_info('Users') WHERE name = 'IsAdmin'";
            if (Convert.ToInt64(check.ExecuteScalar()) > 0)
                return;
        }

        using var add = conn.CreateCommand();
        add.CommandText = "ALTER TABLE Users ADD COLUMN IsAdmin INTEGER NOT NULL DEFAULT 0";
        add.ExecuteNonQuery();
    }
}
