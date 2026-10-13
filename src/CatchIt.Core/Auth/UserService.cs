using CatchIt.Core.Data;
using Microsoft.Data.Sqlite;

namespace CatchIt.Core.Auth;

public class UserService
{
    public const string EmailTakenMessage = "That email is already registered";
    public const string InvalidLoginMessage = "Invalid email or password";
    public const string ConfirmationMessage =
        "Account created. It is not confirmed yet; confirm it from the banner after you log in.";

    private const int SqliteConstraintError = 19;

    private readonly Database _db;
    private readonly Session _session;

    public UserService(Database db, Session session)
    {
        _db = db;
        _session = session;
    }

    public AuthResult Register(string? email, string? password, string name = "")
    {
        if (!Validation.IsValidEmail(email))
            return AuthResult.Fail(Validation.InvalidEmailMessage);
        if (!Validation.IsValidPassword(password))
            return AuthResult.Fail(Validation.PasswordRequirementsMessage);

        var cleanEmail = email!.Trim();
        var hash = BCrypt.Net.BCrypt.HashPassword(password, Seeder.BcryptCost);

        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "INSERT INTO Users (Name, Email, PasswordHash, IsConfirmed) VALUES ($n, $e, $h, 0); " +
            "SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("$n", name.Trim());
        cmd.Parameters.AddWithValue("$e", cleanEmail);
        cmd.Parameters.AddWithValue("$h", hash);

        try
        {
            var id = Convert.ToInt64(cmd.ExecuteScalar());
            return AuthResult.Ok(new User(id, name.Trim(), cleanEmail, false));
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == SqliteConstraintError)
        {
            // Unique email constraint: no duplicate account is created.
            return AuthResult.Fail(EmailTakenMessage);
        }
    }

    public AuthResult Login(string? email, string? password)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
            return AuthResult.Fail(InvalidLoginMessage);

        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, Email, PasswordHash, IsConfirmed FROM Users WHERE Email = $e";
        cmd.Parameters.AddWithValue("$e", email.Trim());

        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
            return AuthResult.Fail(InvalidLoginMessage);

        var hash = reader.GetString(3);
        if (!BCrypt.Net.BCrypt.Verify(password, hash))
            return AuthResult.Fail(InvalidLoginMessage);

        var user = new User(reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetInt64(4) == 1);
        _session.SignIn(user);
        return AuthResult.Ok(user);
    }

    public void Logout() => _session.SignOut();
}
