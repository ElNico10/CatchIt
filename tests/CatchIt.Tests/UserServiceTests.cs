using CatchIt.Core.Auth;
using CatchIt.Core.Data;
using Microsoft.Data.Sqlite;

namespace CatchIt.Tests;

public class UserServiceTests : IDisposable
{
    private const string GoodEmail = "shopper@example.com";
    private const string GoodPassword = "Passw0rd1";

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"catchit-test-{Guid.NewGuid():N}.db");
    private readonly Database _db;
    private readonly Session _session = new();
    private readonly UserService _users;

    public UserServiceTests()
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

    private long UserCount()
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Users";
        return (long)cmd.ExecuteScalar()!;
    }

    // ---- Registration (story #1) ----

    [Fact]
    public void Register_ValidDetails_CreatesUnconfirmedAccount()
    {
        var result = _users.Register(GoodEmail, GoodPassword);

        Assert.True(result.Success);
        Assert.False(result.User!.IsConfirmed);
        Assert.Equal(1, UserCount());
    }

    [Fact]
    public void Register_StoresBcryptHashNotPlainPassword()
    {
        _users.Register(GoodEmail, GoodPassword);

        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT PasswordHash FROM Users WHERE Email = $e";
        cmd.Parameters.AddWithValue("$e", GoodEmail);
        var hash = (string)cmd.ExecuteScalar()!;

        Assert.NotEqual(GoodPassword, hash);
        Assert.StartsWith("$2", hash);
        Assert.Contains("$10$", hash); // cost factor 10
        Assert.True(BCrypt.Net.BCrypt.Verify(GoodPassword, hash));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-email")]
    [InlineData("missing@tld")]
    [InlineData("two@@example.com")]
    [InlineData("spaces in@example.com")]
    public void Register_InvalidEmail_FailsAndCreatesNoAccount(string email)
    {
        var result = _users.Register(email, GoodPassword);

        Assert.False(result.Success);
        Assert.Equal("Please enter a valid email address", result.Error);
        Assert.Equal(0, UserCount());
    }

    [Fact]
    public void Register_NullEmail_Fails()
    {
        var result = _users.Register(null, GoodPassword);

        Assert.False(result.Success);
        Assert.Equal(0, UserCount());
    }

    [Theory]
    [InlineData("")]
    [InlineData("short1")]       // under 8 characters
    [InlineData("onlyletters")]  // no number
    [InlineData("12345678")]     // no letter
    public void Register_InvalidPassword_FailsAndCreatesNoAccount(string password)
    {
        var result = _users.Register(GoodEmail, password);

        Assert.False(result.Success);
        Assert.Equal(Validation.PasswordRequirementsMessage, result.Error);
        Assert.Equal(0, UserCount());
    }

    [Fact]
    public void Register_ExistingEmail_FailsAndCreatesNoDuplicate()
    {
        _users.Register(GoodEmail, GoodPassword);

        var result = _users.Register(GoodEmail, "Different1Pass");

        Assert.False(result.Success);
        Assert.Equal(UserService.EmailTakenMessage, result.Error);
        Assert.Equal(1, UserCount());
    }

    [Fact]
    public void Register_ExistingEmailDifferentCase_IsTreatedAsDuplicate()
    {
        _users.Register(GoodEmail, GoodPassword);

        var result = _users.Register("SHOPPER@Example.com", GoodPassword);

        Assert.False(result.Success);
        Assert.Equal(1, UserCount());
    }

    [Fact]
    public void Register_ValidationIsFast()
    {
        // Story #1: validation errors must appear within one second.
        var watch = System.Diagnostics.Stopwatch.StartNew();
        _users.Register("bad-email", GoodPassword);
        _users.Register(GoodEmail, "short");
        watch.Stop();

        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(1));
    }

    // ---- Login (story #3) ----

    [Fact]
    public void Login_CorrectCredentials_SucceedsAndStartsSession()
    {
        _users.Register(GoodEmail, GoodPassword);

        var result = _users.Login(GoodEmail, GoodPassword);

        Assert.True(result.Success);
        Assert.True(_session.IsLoggedIn);
        Assert.Equal(GoodEmail, _session.CurrentUser!.Email);
    }

    [Fact]
    public void Login_EmailIsCaseInsensitive()
    {
        _users.Register(GoodEmail, GoodPassword);

        var result = _users.Login("Shopper@EXAMPLE.com", GoodPassword);

        Assert.True(result.Success);
    }

    [Fact]
    public void Login_WrongPassword_FailsWithGenericMessage()
    {
        _users.Register(GoodEmail, GoodPassword);

        var result = _users.Login(GoodEmail, "WrongPass1");

        Assert.False(result.Success);
        Assert.Equal("Invalid email or password", result.Error);
        Assert.False(_session.IsLoggedIn);
    }

    [Fact]
    public void Login_UnknownEmail_FailsWithSameGenericMessage()
    {
        var result = _users.Login("nobody@example.com", GoodPassword);

        Assert.False(result.Success);
        Assert.Equal("Invalid email or password", result.Error);
        Assert.False(_session.IsLoggedIn);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData(GoodEmail, "")]
    public void Login_EmptyInput_Fails(string? email, string? password)
    {
        _users.Register(GoodEmail, GoodPassword);

        var result = _users.Login(email, password);

        Assert.False(result.Success);
        Assert.False(_session.IsLoggedIn);
    }

    [Fact]
    public void Login_UnconfirmedAccount_CanStillLogIn()
    {
        _users.Register(GoodEmail, GoodPassword);

        var result = _users.Login(GoodEmail, GoodPassword);

        Assert.True(result.Success);
        Assert.False(result.User!.IsConfirmed);
    }

    [Fact]
    public void Login_SeededDemoUser_Works()
    {
        new Seeder(_db).Run();

        var result = _users.Login("demo@catchit.test", "Demo1234!");

        Assert.True(result.Success);
        Assert.True(result.User!.IsConfirmed);
    }

    // ---- Logout (story #17) ----

    [Fact]
    public void Logout_EndsSession()
    {
        _users.Register(GoodEmail, GoodPassword);
        _users.Login(GoodEmail, GoodPassword);

        _users.Logout();

        Assert.False(_session.IsLoggedIn);
        Assert.Null(_session.CurrentUser);
    }
}
