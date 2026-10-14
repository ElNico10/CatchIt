using CatchIt.Core.Auth;
using CatchIt.Core.Data;
using Microsoft.Data.Sqlite;

namespace CatchIt.Tests;

/// <summary>
/// Tests for <see cref="ConfirmationService"/> and the banner rule on <see cref="Session"/>
/// (user story #2). Each test gets its own throwaway database file.
/// </summary>
/// <remarks>
/// Two things are replaced so the tests are fast and deterministic: the delivery hook just
/// records the email and code it was given (instead of showing a window), and the clock is a
/// field the tests can move forward to simulate time passing.
/// </remarks>
public class ConfirmationServiceTests : IDisposable
{
    private const string Email = "shopper@example.com";
    private const string Password = "Passw0rd1";

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"catchit-test-{Guid.NewGuid():N}.db");
    private readonly Database _db;
    private readonly Session _session = new();
    private readonly UserService _users;
    private readonly ConfirmationService _confirm;

    // Fake clock: tests change this to make codes expire.
    private DateTime _now = new(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc);

    // What the fake delivery hook last received, and how many times it was called.
    private string? _deliveredTo;
    private string? _deliveredCode;
    private int _deliveries;

    public ConfirmationServiceTests()
    {
        _db = new Database(_path);
        _db.Initialize();
        _users = new UserService(_db, _session);
        _confirm = new ConfirmationService(_db, _session,
            (email, code) => { _deliveredTo = email; _deliveredCode = code; _deliveries++; },
            () => _now);
    }

    public void Dispose()
    {
        // SQLite keeps pooled connections open, which would stop the file being deleted.
        SqliteConnection.ClearAllPools();
        if (File.Exists(_path)) File.Delete(_path);
    }

    /// <summary>Creates an unconfirmed account and logs into it, leaving the session ready for confirmation.</summary>
    private void RegisterAndLogin()
    {
        _users.Register(Email, Password);
        _users.Login(Email, Password);
    }

    /// <summary>Reads the confirmed flag straight from the database, bypassing the session.</summary>
    private bool IsConfirmedInDatabase()
    {
        using var conn = _db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT IsConfirmed FROM Users WHERE Email = $e";
        cmd.Parameters.AddWithValue("$e", Email);
        return (long)cmd.ExecuteScalar()! == 1;
    }

    // ---- Banner logic (story #2) ----

    [Fact]
    public void UnconfirmedUser_NeedsConfirmation_AndCanStillUseTheApp()
    {
        RegisterAndLogin();

        Assert.True(_session.NeedsConfirmation);
        Assert.True(Access.CanUse(_session, Feature.Search));
        Assert.True(Access.CanUse(_session, Feature.Watchlist));
    }

    [Fact]
    public void ConfirmedUser_DoesNotNeedConfirmation()
    {
        // The seeded demo account is created already confirmed.
        new Seeder(_db).Run();
        _users.Login("demo@catchit.test", "Demo1234!");

        Assert.False(_session.NeedsConfirmation);
    }

    [Fact]
    public void GuestAndSignedOut_DoNotNeedConfirmation()
    {
        Assert.False(_session.NeedsConfirmation);

        _session.EnterGuestMode();
        Assert.False(_session.NeedsConfirmation);
    }

    // ---- Requesting a code (simulated mechanism) ----

    [Fact]
    public void RequestCode_DeliversSixDigitCodeToTheUsersEmail()
    {
        RegisterAndLogin();

        var result = _confirm.RequestCode();

        Assert.True(result.Success);
        Assert.Equal(1, _deliveries);
        Assert.Equal(Email, _deliveredTo);
        Assert.Matches(@"^\d{6}$", _deliveredCode);
    }

    [Fact]
    public void RequestCode_WhenNotLoggedIn_Fails()
    {
        Assert.False(_confirm.RequestCode().Success);

        // A guest has no account to confirm, so the result is the same.
        _session.EnterGuestMode();
        var result = _confirm.RequestCode();

        Assert.False(result.Success);
        Assert.Equal(ConfirmationService.NotLoggedInMessage, result.Error);
        Assert.Equal(0, _deliveries);
    }

    [Fact]
    public void RequestCode_WhenAlreadyConfirmed_Fails()
    {
        new Seeder(_db).Run();
        _users.Login("demo@catchit.test", "Demo1234!");

        var result = _confirm.RequestCode();

        Assert.False(result.Success);
        Assert.Equal(ConfirmationService.AlreadyConfirmedMessage, result.Error);
        Assert.Equal(0, _deliveries);
    }

    // ---- Confirming ----

    [Fact]
    public void Confirm_CorrectCode_ConfirmsAccountInDatabaseAndSession()
    {
        RegisterAndLogin();
        _confirm.RequestCode();

        var result = _confirm.Confirm(_deliveredCode);

        Assert.True(result.Success);
        Assert.True(IsConfirmedInDatabase());
        Assert.True(_session.CurrentUser!.IsConfirmed);
        Assert.False(_session.NeedsConfirmation);
    }

    [Fact]
    public void Confirm_StaysConfirmedAfterLoggingInAgain()
    {
        RegisterAndLogin();
        _confirm.RequestCode();
        _confirm.Confirm(_deliveredCode);
        _users.Logout();

        var login = _users.Login(Email, Password);

        Assert.True(login.User!.IsConfirmed);
        Assert.False(_session.NeedsConfirmation);
    }

    [Fact]
    public void Confirm_IgnoresSurroundingWhitespace()
    {
        RegisterAndLogin();
        _confirm.RequestCode();

        Assert.True(_confirm.Confirm($"  {_deliveredCode} ").Success);
    }

    [Fact]
    public void Confirm_WrongCode_FailsAndAccountStaysUnconfirmed()
    {
        RegisterAndLogin();
        _confirm.RequestCode();

        // Pick a code that is guaranteed to differ from the real one.
        var wrong = _deliveredCode == "000000" ? "000001" : "000000";

        var result = _confirm.Confirm(wrong);

        Assert.False(result.Success);
        Assert.Equal(ConfirmationService.WrongCodeMessage, result.Error);
        Assert.False(IsConfirmedInDatabase());
        Assert.True(_session.NeedsConfirmation);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    public void Confirm_EmptyOrJunkCode_Fails(string? code)
    {
        RegisterAndLogin();
        _confirm.RequestCode();

        Assert.False(_confirm.Confirm(code).Success);
        Assert.False(IsConfirmedInDatabase());
    }

    [Fact]
    public void Confirm_WithoutRequestingACode_Fails()
    {
        RegisterAndLogin();

        var result = _confirm.Confirm("123456");

        Assert.False(result.Success);
        Assert.Equal(ConfirmationService.NoCodeMessage, result.Error);
    }

    [Fact]
    public void Confirm_ExpiredCode_FailsEvenIfCorrect()
    {
        RegisterAndLogin();
        _confirm.RequestCode();

        // Move the fake clock one second past the code's lifetime.
        _now += ConfirmationService.CodeLifetime + TimeSpan.FromSeconds(1);

        var result = _confirm.Confirm(_deliveredCode);

        Assert.False(result.Success);
        Assert.Equal(ConfirmationService.ExpiredMessage, result.Error);
        Assert.False(IsConfirmedInDatabase());
    }

    [Fact]
    public void Confirm_CodeStillWorksJustBeforeExpiry()
    {
        RegisterAndLogin();
        _confirm.RequestCode();

        // The boundary case: one second before the deadline, the code must still be accepted.
        _now += ConfirmationService.CodeLifetime - TimeSpan.FromSeconds(1);

        Assert.True(_confirm.Confirm(_deliveredCode).Success);
    }

    /// <summary>
    /// After the allowed number of wrong guesses the code is discarded, so even the correct
    /// code stops working and a new one has to be requested.
    /// </summary>
    [Fact]
    public void Confirm_TooManyWrongAttempts_InvalidatesTheCode()
    {
        RegisterAndLogin();
        _confirm.RequestCode();
        var realCode = _deliveredCode;
        var wrong = realCode == "000000" ? "000001" : "000000";

        // The first (max - 1) wrong guesses get the ordinary message; the last one locks the code.
        for (var i = 1; i < ConfirmationService.MaxWrongAttempts; i++)
            Assert.Equal(ConfirmationService.WrongCodeMessage, _confirm.Confirm(wrong).Error);
        Assert.Equal(ConfirmationService.TooManyAttemptsMessage, _confirm.Confirm(wrong).Error);

        // Even the right code is rejected now; a new one must be requested.
        Assert.Equal(ConfirmationService.NoCodeMessage, _confirm.Confirm(realCode).Error);
        _confirm.RequestCode();
        Assert.True(_confirm.Confirm(_deliveredCode).Success);
    }

    [Fact]
    public void RequestCode_Again_ReplacesTheEarlierCode()
    {
        RegisterAndLogin();
        _confirm.RequestCode();
        var first = _deliveredCode;

        // Keep asking until the new code differs from the first (they match only 1 time in a million).
        string? second;
        do { _confirm.RequestCode(); second = _deliveredCode; } while (second == first);

        Assert.False(_confirm.Confirm(first).Success);
        Assert.True(_confirm.Confirm(second).Success);
    }

    [Fact]
    public void Confirm_WhenNotLoggedIn_Fails()
    {
        var result = _confirm.Confirm("123456");

        Assert.False(result.Success);
        Assert.Equal(ConfirmationService.NotLoggedInMessage, result.Error);
    }
}
