using CatchIt.Core.Data;
using Microsoft.Data.Sqlite;

namespace CatchIt.Core.Auth;

public class UserService
{
    public const string EmailTakenMessage = "That email is already registered";
    public const string InvalidLoginMessage = "Invalid email or password";
    public const string ConfirmationMessage =
        "Account created. It is not confirmed yet; confirm it from the banner after you log in.";


    public const string ChangePasswordLoginRequiredMessage = "Please log in to change your password";
    public const string CurrentPasswordIncorrectMessage = "Current password is incorrect";
    public const string PasswordsDoNotMatchMessage = "New passwords do not match";
    public const string PasswordUnchangedMessage = "New password must be different from your current password";
    public const string PasswordChangedMessage = "Your password has been changed";

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


   
    /// Changes the logged-in user's password (story #16, "Change password with valid data").
    /// The change is only made when the user is logged in, proves who they are with their
    /// current password, types the new password the same way twice, and the new password
    /// meets the same rules as registration and differs from the old one.
    
    public AuthResult ChangePassword(string? currentPassword, string? newPassword, string? confirmNewPassword)
    {
        var user = _session.CurrentUser;
        if (user == null)
            return AuthResult.Fail(ChangePasswordLoginRequiredMessage);

        using var conn = _db.Open();

        string? storedHash;
        using (var select = conn.CreateCommand())
        {
            select.CommandText = "SELECT PasswordHash FROM Users WHERE Id = $id";
            select.Parameters.AddWithValue("$id", user.Id);
            storedHash = select.ExecuteScalar() as string;
        }

        // The account no longer exists, so the session is stale.
        if (storedHash == null)
        {
            _session.SignOut();
            return AuthResult.Fail(ChangePasswordLoginRequiredMessage);
        }

        if (string.IsNullOrEmpty(currentPassword) || !BCrypt.Net.BCrypt.Verify(currentPassword, storedHash))
            return AuthResult.Fail(CurrentPasswordIncorrectMessage);

        if (!Validation.IsValidPassword(newPassword))
            return AuthResult.Fail(Validation.PasswordRequirementsMessage);

        if (newPassword != confirmNewPassword)
            return AuthResult.Fail(PasswordsDoNotMatchMessage);

        if (newPassword == currentPassword)
            return AuthResult.Fail(PasswordUnchangedMessage);

        var newHash = BCrypt.Net.BCrypt.HashPassword(newPassword!, Seeder.BcryptCost);

        using (var update = conn.CreateCommand())
        {
            // Clearing ResetToken cancels any password-reset link that was issued before this change.
            update.CommandText = "UPDATE Users SET PasswordHash = $h, ResetToken = NULL WHERE Id = $id";
            update.Parameters.AddWithValue("$h", newHash);
            update.Parameters.AddWithValue("$id", user.Id);
            update.ExecuteNonQuery();
        }

        return AuthResult.Ok(user);
    }


}
