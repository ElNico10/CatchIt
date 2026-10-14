using CatchIt.Core.Auth;
using CatchIt.Core.Data;
using Microsoft.Data.Sqlite;

namespace CatchIt.Tests;

/// <summary>
/// Tests for <see cref="ProfileService"/> (user story #16, name and email only).
/// Each test gets its own throwaway database file.
/// </summary>
public class ProfileServiceTests : IDisposable
{
    private const string Email = "shopper@example.com";
    private const string Password = "Passw0rd1";

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"catchit-test-{Guid.NewGuid():N}.db");
    private readonly Database _db;
    private readonly Session _session = new();
    private readonly UserService _users;
    private readonly ProfileService _profile;

    public ProfileServiceTests()
    {
        _db = new Database(_path);
        _db.Initialize();
        _users = new UserService(_db, _session);
        _profile = new ProfileService(_db, _session);
    }

    public void Dispose()
    {
        // SQLite keeps pooled connections open, which would stop the file being deleted.
        SqliteConnection.ClearAllPools();
        if (File.Exists(_path)) File.Delete(_path);
    }

    /// <summary>Registers an account and logs into it, leaving the session ready for profile edits.</summary>
    private void RegisterAndLogin(string email = Email, string name = "Original Name")
    {
        _users.Register(email, Password, name);
        _users.Login(email, Password);
    }

    /// <summary>Reads the saved name and email straight from the database, bypassing the session.</summary>
    private (string Name, string Email) SavedProfile(long userId)
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Name, Email FROM Users WHERE Id = $id";
        cmd.Parameters.AddWithValue("$id", userId);
        using var reader = cmd.ExecuteReader();
        reader.Read();
        return (reader.GetString(0), reader.GetString(1));
    }

    // ---- Successful updates ----

    [Fact]
    public void UpdateProfile_ValidData_SavesToDatabaseAndSession()
    {
        RegisterAndLogin();
        var id = _session.CurrentUser!.Id;

        var result = _profile.UpdateProfile("New Name", "new@example.com");

        Assert.True(result.Success);
        Assert.Equal(("New Name", "new@example.com"), SavedProfile(id));
        Assert.Equal("New Name", _session.CurrentUser!.Name);
        Assert.Equal("new@example.com", _session.CurrentUser.Email);
    }

    [Fact]
    public void UpdateProfile_TrimsWhitespace()
    {
        RegisterAndLogin();
        var id = _session.CurrentUser!.Id;

        _profile.UpdateProfile("  Spaced Name  ", "  spaced@example.com  ");

        Assert.Equal(("Spaced Name", "spaced@example.com"), SavedProfile(id));
    }

    [Fact]
    public void UpdateProfile_EmptyNameIsAllowed()
    {
        RegisterAndLogin();

        var result = _profile.UpdateProfile("", Email);

        Assert.True(result.Success);
        Assert.Equal("", _session.CurrentUser!.Name);
    }

    [Fact]
    public void UpdateProfile_KeepingTheSameEmail_IsNotADuplicate()
    {
        RegisterAndLogin();

        // Changing only the name must not trip the unique-email rule against the user's own row.
        var result = _profile.UpdateProfile("Different Name", Email);

        Assert.True(result.Success);
    }

    [Fact]
    public void UpdateProfile_ChangingOnlyTheEmailCase_Succeeds()
    {
        RegisterAndLogin();

        var result = _profile.UpdateProfile("Original Name", "SHOPPER@Example.com");

        Assert.True(result.Success);
        Assert.Equal("SHOPPER@Example.com", _session.CurrentUser!.Email);
    }

    [Fact]
    public void UpdateProfile_UserCanLogInWithTheNewEmail()
    {
        RegisterAndLogin();
        _profile.UpdateProfile("Original Name", "moved@example.com");
        _users.Logout();

        var login = _users.Login("moved@example.com", Password);

        Assert.True(login.Success);
    }

    [Fact]
    public void UpdateProfile_ChangingEmail_KeepsConfirmedStatus()
    {
        // The seeded demo account is created already confirmed.
        new Seeder(_db).Run();
        _users.Login("demo@catchit.test", "Demo1234!");

        _profile.UpdateProfile("Demo User", "demo.moved@catchit.test");

        Assert.True(_session.CurrentUser!.IsConfirmed);
        Assert.False(_session.NeedsConfirmation);
    }

    // ---- Rejected updates: nothing may change ----

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-email")]
    [InlineData("missing@tld")]
    [InlineData("two@@example.com")]
    public void UpdateProfile_InvalidEmail_FailsAndChangesNothing(string email)
    {
        RegisterAndLogin();
        var id = _session.CurrentUser!.Id;

        var result = _profile.UpdateProfile("Changed Name", email);

        Assert.False(result.Success);
        Assert.Equal("Please enter a valid email address", result.Error);
        Assert.Equal(("Original Name", Email), SavedProfile(id));
        Assert.Equal("Original Name", _session.CurrentUser!.Name);
    }

    [Fact]
    public void UpdateProfile_EmailBelongingToAnotherAccount_FailsAndChangesNothing()
    {
        _users.Register("other@example.com", Password, "Other Person");
        RegisterAndLogin();
        var id = _session.CurrentUser!.Id;

        var result = _profile.UpdateProfile("Changed Name", "other@example.com");

        Assert.False(result.Success);
        Assert.Equal(UserService.EmailTakenMessage, result.Error);
        Assert.Equal(("Original Name", Email), SavedProfile(id));
        Assert.Equal(Email, _session.CurrentUser!.Email);
    }

    [Fact]
    public void UpdateProfile_EmailOfAnotherAccountInDifferentCase_IsStillADuplicate()
    {
        _users.Register("other@example.com", Password);
        RegisterAndLogin();

        var result = _profile.UpdateProfile("Original Name", "OTHER@example.com");

        Assert.False(result.Success);
    }

    [Fact]
    public void UpdateProfile_NameTooLong_Fails()
    {
        RegisterAndLogin();
        var id = _session.CurrentUser!.Id;

        var result = _profile.UpdateProfile(new string('x', ProfileService.MaxNameLength + 1), Email);

        Assert.False(result.Success);
        Assert.Equal(ProfileService.NameTooLongMessage, result.Error);
        Assert.Equal(("Original Name", Email), SavedProfile(id));
    }

    [Fact]
    public void UpdateProfile_NameAtTheLimit_Succeeds()
    {
        RegisterAndLogin();

        var result = _profile.UpdateProfile(new string('x', ProfileService.MaxNameLength), Email);

        Assert.True(result.Success);
    }

    [Fact]
    public void UpdateProfile_NullInputs_FailCleanly()
    {
        RegisterAndLogin();

        Assert.False(_profile.UpdateProfile("Name", null).Success);
        Assert.True(_profile.UpdateProfile(null, Email).Success); // a null name is treated as empty
    }

    // ---- Who may update ----

    [Fact]
    public void UpdateProfile_WhenSignedOut_Fails()
    {
        var result = _profile.UpdateProfile("Name", Email);

        Assert.False(result.Success);
        Assert.Equal(ProfileService.NotLoggedInMessage, result.Error);
    }

    [Fact]
    public void UpdateProfile_AsGuest_Fails()
    {
        _session.EnterGuestMode();

        var result = _profile.UpdateProfile("Name", Email);

        Assert.False(result.Success);
        Assert.Equal(ProfileService.NotLoggedInMessage, result.Error);
    }

    [Fact]
    public void UpdateProfile_OnlyChangesTheLoggedInUsersRow()
    {
        _users.Register("other@example.com", Password, "Other Person");
        RegisterAndLogin();

        _profile.UpdateProfile("Changed Name", "changed@example.com");

        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Name FROM Users WHERE Email = 'other@example.com'";
        Assert.Equal("Other Person", (string)cmd.ExecuteScalar()!);
    }
}
