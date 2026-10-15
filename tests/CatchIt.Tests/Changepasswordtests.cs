using CatchIt.Core.Auth;
using CatchIt.Core.Data;
using Microsoft.Data.Sqlite;

namespace CatchIt.Tests;

/// <summary>
/// Story #16: "Change password with valid data", plus the rules that keep a password change secure.
/// </summary>
public class ChangePasswordTests : IDisposable
{
    private const string Email = "shopper@example.com";
    private const string OldPassword = "Passw0rd1";
    private const string NewPassword = "NewPassw0rd2";

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"catchit-test-{Guid.NewGuid():N}.db");
    private readonly Database _db;
    private readonly Session _session = new();
    private readonly UserService _users;

    public ChangePasswordTests()
    {
        _db = new Database(_path);
        _db.Initialize();
        _users = new UserService(_db, _session);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(_path)) File.Delete(_path);
    }

    private void RegisterAndLogIn()
    {
        _users.Register(Email, OldPassword);
        _users.Login(Email, OldPassword);
    }

    private string StoredHash()
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT PasswordHash FROM Users WHERE Email = $e";
        cmd.Parameters.AddWithValue("$e", Email);
        return (string)cmd.ExecuteScalar()!;
    }

    private void SetResetToken(string? token)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE Users SET ResetToken = $t WHERE Email = $e";
        cmd.Parameters.AddWithValue("$t", (object?)token ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$e", Email);
        cmd.ExecuteNonQuery();
    }

    private object? StoredResetToken()
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT ResetToken FROM Users WHERE Email = $e";
        cmd.Parameters.AddWithValue("$e", Email);
        return cmd.ExecuteScalar();
    }

    // ---- Successful change ----

    [Fact]
    public void ChangePassword_ValidData_Succeeds()
    {
        RegisterAndLogIn();

        var result = _users.ChangePassword(OldPassword, NewPassword, NewPassword);

        Assert.True(result.Success);
        Assert.Null(result.Error);
    }

    [Fact]
    public void ChangePassword_ValidData_CanLogInWithNewPasswordOnly()
    {
        RegisterAndLogIn();
        _users.ChangePassword(OldPassword, NewPassword, NewPassword);
        _users.Logout();

        Assert.False(_users.Login(Email, OldPassword).Success);
        Assert.True(_users.Login(Email, NewPassword).Success);
    }

    [Fact]
    public void ChangePassword_StoresBcryptHashWithCost10()
    {
        RegisterAndLogIn();
        _users.ChangePassword(OldPassword, NewPassword, NewPassword);

        var hash = StoredHash();

        Assert.NotEqual(NewPassword, hash);
        Assert.StartsWith("$2", hash);
        Assert.Contains("$10$", hash);
        Assert.True(BCrypt.Net.BCrypt.Verify(NewPassword, hash));
    }

    [Fact]
    public void ChangePassword_ValidData_KeepsUserLoggedIn()
    {
        RegisterAndLogIn();

        _users.ChangePassword(OldPassword, NewPassword, NewPassword);

        Assert.True(_session.IsLoggedIn);
        Assert.Equal(Email, _session.CurrentUser!.Email);
    }

    [Fact]
    public void ChangePassword_ValidData_CancelsOutstandingResetLink()
    {
        RegisterAndLogIn();
        SetResetToken("old-reset-token");

        _users.ChangePassword(OldPassword, NewPassword, NewPassword);

        Assert.Equal(DBNull.Value, StoredResetToken());
    }

    // ---- Must be logged in ----

    [Fact]
    public void ChangePassword_NotLoggedIn_Fails()
    {
        _users.Register(Email, OldPassword);
        var hashBefore = StoredHash();

        var result = _users.ChangePassword(OldPassword, NewPassword, NewPassword);

        Assert.False(result.Success);
        Assert.Equal(UserService.ChangePasswordLoginRequiredMessage, result.Error);
        Assert.Equal(hashBefore, StoredHash());
    }

    [Fact]
    public void ChangePassword_AsGuest_Fails()
    {
        _users.Register(Email, OldPassword);
        _session.EnterGuestMode();

        var result = _users.ChangePassword(OldPassword, NewPassword, NewPassword);

        Assert.False(result.Success);
        Assert.Equal(UserService.ChangePasswordLoginRequiredMessage, result.Error);
    }

    [Fact]
    public void ChangePassword_AfterLogout_Fails()
    {
        RegisterAndLogIn();
        _users.Logout();

        var result = _users.ChangePassword(OldPassword, NewPassword, NewPassword);

        Assert.False(result.Success);
        Assert.Equal(UserService.ChangePasswordLoginRequiredMessage, result.Error);
    }

    // ---- Must prove the current password ----

    [Theory]
    [InlineData("WrongPass1")]
    [InlineData("")]
    [InlineData((string?)null)]
    public void ChangePassword_WrongCurrentPassword_FailsAndKeepsOldPassword(string? current)
    {
        RegisterAndLogIn();
        var hashBefore = StoredHash();

        var result = _users.ChangePassword(current, NewPassword, NewPassword);

        Assert.False(result.Success);
        Assert.Equal(UserService.CurrentPasswordIncorrectMessage, result.Error);
        Assert.Equal(hashBefore, StoredHash());
    }

    // ---- New password must meet the rules ----

    [Theory]
    [InlineData("")]
    [InlineData("short1")]       // under 8 characters
    [InlineData("onlyletters")]  // no number
    [InlineData("12345678")]     // no letter
    [InlineData((string?)null)]
    public void ChangePassword_WeakNewPassword_FailsAndKeepsOldPassword(string? weak)
    {
        RegisterAndLogIn();
        var hashBefore = StoredHash();

        var result = _users.ChangePassword(OldPassword, weak, weak);

        Assert.False(result.Success);
        Assert.Equal(Validation.PasswordRequirementsMessage, result.Error);
        Assert.Equal(hashBefore, StoredHash());
    }

    [Fact]
    public void ChangePassword_ConfirmationDoesNotMatch_Fails()
    {
        RegisterAndLogIn();
        var hashBefore = StoredHash();

        var result = _users.ChangePassword(OldPassword, NewPassword, "NewPassw0rd3");

        Assert.False(result.Success);
        Assert.Equal(UserService.PasswordsDoNotMatchMessage, result.Error);
        Assert.Equal(hashBefore, StoredHash());
    }

    [Fact]
    public void ChangePassword_ConfirmationMissing_Fails()
    {
        RegisterAndLogIn();

        var result = _users.ChangePassword(OldPassword, NewPassword, null);

        Assert.False(result.Success);
        Assert.Equal(UserService.PasswordsDoNotMatchMessage, result.Error);
    }

    [Fact]
    public void ChangePassword_SameAsCurrent_Fails()
    {
        RegisterAndLogIn();
        var hashBefore = StoredHash();

        var result = _users.ChangePassword(OldPassword, OldPassword, OldPassword);

        Assert.False(result.Success);
        Assert.Equal(UserService.PasswordUnchangedMessage, result.Error);
        Assert.Equal(hashBefore, StoredHash());
    }

    // ---- Seeded demo account ----

    [Fact]
    public void ChangePassword_SeededDemoUser_Works()
    {
        new Seeder(_db).Run();
        _users.Login("demo@catchit.test", "Demo1234!");

        var result = _users.ChangePassword("Demo1234!", NewPassword, NewPassword);
        _users.Logout();

        Assert.True(result.Success);
        Assert.True(_users.Login("demo@catchit.test", NewPassword).Success);
    }
}