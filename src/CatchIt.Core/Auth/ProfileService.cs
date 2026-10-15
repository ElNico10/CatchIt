using CatchIt.Core.Data;
using Microsoft.Data.Sqlite;

namespace CatchIt.Core.Auth;

/// <summary>
/// Lets a logged-in user change their name and email (user story #16).
/// </summary>
/// <remarks>
/// Password changes are deliberately not handled here: they depend on how passwords are
/// hashed and validated, which is covered by the secure password storage work.
/// Changing the email address does not reset the account's confirmed status.
/// </remarks>
public class ProfileService
{
    /// <summary>Longest name accepted, to keep the top bar and database tidy.</summary>
    public const int MaxNameLength = 100;

    public const string SuccessMessage = "Profile updated";
    public const string NotLoggedInMessage = "Please log in to update your profile";
    public const string NameTooLongMessage = "Name must be 100 characters or fewer";

    // SQLite's result code for a violated constraint (here, the unique email rule).
    private const int SqliteConstraintError = 19;

    private readonly Database _db;
    private readonly Session _session;

    /// <summary>Creates the service.</summary>
    /// <param name="db">Where the profile is saved.</param>
    /// <param name="session">Identifies the user being updated, and is refreshed after a successful change.</param>
    public ProfileService(Database db, Session session)
    {
        _db = db;
        _session = session;
    }

    /// <summary>Saves a new name and email for the logged-in user.</summary>
    /// <param name="name">The new display name. May be empty; surrounding whitespace is removed.</param>
    /// <param name="email">The new email address. Must be a valid format and not belong to another account.</param>
    /// <returns>
    /// Success with the updated user, or a failure whose Error is the message to show. On failure
    /// nothing is changed, in the database or in the session.
    /// </returns>
    public AuthResult UpdateProfile(string? name, string? email)
    {
        var user = _session.CurrentUser;
        if (user == null)
            return AuthResult.Fail(NotLoggedInMessage);

        if (!Validation.IsValidEmail(email))
            return AuthResult.Fail(Validation.InvalidEmailMessage);

        var cleanName = (name ?? string.Empty).Trim();
        if (cleanName.Length > MaxNameLength)
            return AuthResult.Fail(NameTooLongMessage);

        var cleanEmail = email!.Trim();

        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE Users SET Name = $n, Email = $e WHERE Id = $id";
        cmd.Parameters.AddWithValue("$n", cleanName);
        cmd.Parameters.AddWithValue("$e", cleanEmail);
        cmd.Parameters.AddWithValue("$id", user.Id);

        try
        {
            cmd.ExecuteNonQuery();
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == SqliteConstraintError)
        {
            // The email column is unique, so this means another account already uses it.
            return AuthResult.Fail(UserService.EmailTakenMessage);
        }

        // The session holds an immutable User record, so replace it with an updated copy; the
        // top bar and any other screen reading the session then show the new details.
        var updated = user with { Name = cleanName, Email = cleanEmail };
        _session.SignIn(updated);
        return AuthResult.Ok(updated);
    }
}
