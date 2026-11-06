using CatchIt.Core.Auth;

namespace CatchIt.Tests;

/// <summary>
/// Tests for <see cref="PasswordResetService"/> (user story #4, "Reset a forgotten password").
/// Each test gets its own throwaway database file.
/// </summary>
/// <remarks>
/// As in <see cref="ConfirmationServiceTests"/>, the delivery hook just records the email and code
/// (instead of showing a window), and the clock is a field the tests move forward.
/// </remarks>
public class PasswordResetServiceTests : TempDatabaseTests
{
    private const string Email = "shopper@example.com";
    private const string OldPassword = "Passw0rd1";
    private const string NewPassword = "NewPassw0rd2";

    private readonly Session _session = new();
    private readonly UserService _users;
    private readonly PasswordResetService _reset;

    private DateTime _now = new(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);

    private string? _deliveredTo;
    private string? _deliveredCode;
    private int _deliveries;

    public PasswordResetServiceTests()
    {
        _users = new UserService(Db, _session);
        _reset = new PasswordResetService(Db,
            (email, code) => { _deliveredTo = email; _deliveredCode = code; _deliveries++; },
            () => _now);
        _users.Register(Email, OldPassword);
    }

    /// <summary>A 6-digit code that is guaranteed not to be the one that was delivered.</summary>
    private string WrongCode() => _deliveredCode == "000000" ? "111111" : "000000";

    private object? StoredResetToken()
    {
        using var conn = Db.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT ResetToken FROM Users WHERE Email = $e";
        cmd.Parameters.AddWithValue("$e", Email);
        return cmd.ExecuteScalar();
    }

    // ---- Scenario 1: reset requested for a registered email ----

    [Fact]
    public void RequestReset_RegisteredEmail_DeliversSixDigitCode()
    {
        var result = _reset.RequestReset(Email);

        Assert.True(result.Success);
        Assert.Equal(1, _deliveries);
        Assert.Equal(Email, _deliveredTo);
        Assert.Matches("^[0-9]{6}$", _deliveredCode!);
    }

    [Fact]
    public void RequestReset_IgnoresCaseAndSpaces()
    {
        var result = _reset.RequestReset("  SHOPPER@Example.com ");

        Assert.True(result.Success);
        Assert.Equal(Email, _deliveredTo);
    }

    [Fact]
    public void RequestReset_StoresOnlyAHashOfTheCode()
    {
        _reset.RequestReset(Email);

        var stored = Assert.IsType<string>(StoredResetToken());
        Assert.NotEqual(_deliveredCode, stored);
        Assert.Equal(64, stored.Length); // SHA-256 as hex
    }

    // ---- Scenario 2: reset requested for an unregistered email ----

    [Fact]
    public void RequestReset_UnregisteredEmail_ShowsEmailNotFound()
    {
        var result = _reset.RequestReset("nobody@example.com");

        Assert.False(result.Success);
        Assert.Equal("Email not found", result.Error);
        Assert.Equal(0, _deliveries);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-an-email")]
    public void RequestReset_InvalidEmail_IsRejected(string? email)
    {
        var result = _reset.RequestReset(email);

        Assert.False(result.Success);
        Assert.Equal(Validation.InvalidEmailMessage, result.Error);
        Assert.Equal(0, _deliveries);
    }

    // ---- Scenario 3: password changed using a valid code ----

    [Fact]
    public void ResetPassword_ValidCode_UpdatesPassword_AndNewPasswordLogsIn()
    {
        _reset.RequestReset(Email);

        var result = _reset.ResetPassword(Email, _deliveredCode, NewPassword, NewPassword);

        Assert.True(result.Success);
        Assert.True(_users.Login(Email, NewPassword).Success);
        _users.Logout();
        Assert.False(_users.Login(Email, OldPassword).Success);
    }

    [Fact]
    public void ResetPassword_CodeIsSingleUse()
    {
        _reset.RequestReset(Email);
        var code = _deliveredCode;
        _reset.ResetPassword(Email, code, NewPassword, NewPassword);

        var again = _reset.ResetPassword(Email, code, "Another1pass", "Another1pass");

        Assert.False(again.Success);
        Assert.Equal(PasswordResetService.NoCodeMessage, again.Error);
        Assert.Equal(DBNull.Value, StoredResetToken());
    }

    [Fact]
    public void ResetPassword_NewRequest_ReplacesOldCode()
    {
        _reset.RequestReset(Email);
        var first = _deliveredCode;
        do { _reset.RequestReset(Email); } while (_deliveredCode == first); // codes are random; rare repeat

        var result = _reset.ResetPassword(Email, first, NewPassword, NewPassword);

        Assert.False(result.Success);
        Assert.Equal(PasswordResetService.WrongCodeMessage, result.Error);
    }

    // ---- Rejections ----

    [Fact]
    public void ResetPassword_WithoutRequestingCode_Fails()
    {
        var result = _reset.ResetPassword(Email, "123456", NewPassword, NewPassword);

        Assert.False(result.Success);
        Assert.Equal(PasswordResetService.NoCodeMessage, result.Error);
    }

    [Fact]
    public void ResetPassword_WrongCode_FailsAndKeepsOldPassword()
    {
        _reset.RequestReset(Email);

        var result = _reset.ResetPassword(Email, WrongCode(), NewPassword, NewPassword);

        Assert.False(result.Success);
        Assert.Equal(PasswordResetService.WrongCodeMessage, result.Error);
        Assert.True(_users.Login(Email, OldPassword).Success);
    }

    [Fact]
    public void ResetPassword_TooManyWrongCodes_DiscardsCode()
    {
        _reset.RequestReset(Email);
        var code = _deliveredCode;

        AuthResult last = null!;
        for (var i = 0; i < PasswordResetService.MaxWrongAttempts; i++)
            last = _reset.ResetPassword(Email, WrongCode(), NewPassword, NewPassword);

        Assert.Equal(PasswordResetService.TooManyAttemptsMessage, last.Error);

        // Even the right code no longer works.
        var right = _reset.ResetPassword(Email, code, NewPassword, NewPassword);
        Assert.Equal(PasswordResetService.NoCodeMessage, right.Error);
    }

    [Fact]
    public void ResetPassword_ExpiredCode_Fails()
    {
        _reset.RequestReset(Email);
        _now += PasswordResetService.CodeLifetime + TimeSpan.FromSeconds(1);

        var result = _reset.ResetPassword(Email, _deliveredCode, NewPassword, NewPassword);

        Assert.False(result.Success);
        Assert.Equal(PasswordResetService.ExpiredMessage, result.Error);
    }

    [Fact]
    public void ResetPassword_JustBeforeExpiry_Succeeds()
    {
        _reset.RequestReset(Email);
        _now += PasswordResetService.CodeLifetime - TimeSpan.FromSeconds(1);

        Assert.True(_reset.ResetPassword(Email, _deliveredCode, NewPassword, NewPassword).Success);
    }

    [Fact]
    public void ResetPassword_WeakPassword_Fails_ButCodeStillWorks()
    {
        _reset.RequestReset(Email);

        var weak = _reset.ResetPassword(Email, _deliveredCode, "short", "short");
        Assert.Equal(Validation.PasswordRequirementsMessage, weak.Error);

        Assert.True(_reset.ResetPassword(Email, _deliveredCode, NewPassword, NewPassword).Success);
    }

    [Fact]
    public void ResetPassword_MismatchedPasswords_Fails()
    {
        _reset.RequestReset(Email);

        var result = _reset.ResetPassword(Email, _deliveredCode, NewPassword, "Different1pass");

        Assert.False(result.Success);
        Assert.Equal(PasswordResetService.PasswordsDoNotMatchMessage, result.Error);
    }

    [Fact]
    public void ChangingPassword_CancelsPendingReset()
    {
        _reset.RequestReset(Email);
        var code = _deliveredCode;
        _users.Login(Email, OldPassword);
        _users.ChangePassword(OldPassword, NewPassword, NewPassword);

        var result = _reset.ResetPassword(Email, code, "Another1pass", "Another1pass");

        Assert.Equal(PasswordResetService.NoCodeMessage, result.Error);
    }
}
