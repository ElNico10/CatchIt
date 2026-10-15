using CatchIt.Core.Auth;

namespace CatchIt.Tests;

public class AccessTests
{
    private static readonly User TestUser = new(1, "Test", "test@example.com", false);

    private static Session SignedOut() => new();

    private static Session Guest()
    {
        var s = new Session();
        s.EnterGuestMode();
        return s;
    }

    private static Session LoggedIn()
    {
        var s = new Session();
        s.SignIn(TestUser);
        return s;
    }

    public static IEnumerable<object[]> BasicFeatures() => new[]
    {
        new object[] { Feature.Search },
        new object[] { Feature.PriceDisplay },
        new object[] { Feature.PriceHistory },
        new object[] { Feature.SaleFlags },
        new object[] { Feature.BrandFilter },
        new object[] { Feature.SellerFilter },
        new object[] { Feature.Alternatives },
    };

    public static IEnumerable<object[]> AccountFeatures() => new[]
    {
        new object[] { Feature.Watchlist },
        new object[] { Feature.TargetPrice },
        new object[] { Feature.Dashboard },
        new object[] { Feature.Profile },
    };

    // ---- Session states ----

    [Fact]
    public void NewSession_IsSignedOut()
    {
        var s = SignedOut();

        Assert.False(s.IsActive);
        Assert.False(s.IsGuest);
        Assert.False(s.IsLoggedIn);
    }

    [Fact]
    public void EnterGuestMode_MakesGuestSession()
    {
        var s = Guest();

        Assert.True(s.IsGuest);
        Assert.True(s.IsActive);
        Assert.False(s.IsLoggedIn);
        Assert.Null(s.CurrentUser);
    }

    [Fact]
    public void SignIn_ReplacesGuestSession()
    {
        var s = Guest();

        s.SignIn(TestUser);

        Assert.True(s.IsLoggedIn);
        Assert.False(s.IsGuest);
    }

    [Fact]
    public void SignOut_EndsGuestSession()
    {
        var s = Guest();

        s.SignOut();

        Assert.False(s.IsActive);
        Assert.False(s.IsGuest);
    }

    [Fact]
    public void SignOut_EndsLoggedInSession()
    {
        var s = LoggedIn();

        s.SignOut();

        Assert.False(s.IsActive);
        Assert.Null(s.CurrentUser);
    }

    // ---- Guests: basic features yes, account features no ----

    [Theory]
    [MemberData(nameof(BasicFeatures))]
    public void Guest_CanUseBasicFeatures(Feature feature)
    {
        Assert.True(Access.CanUse(Guest(), feature));
    }

    [Theory]
    [MemberData(nameof(AccountFeatures))]
    public void Guest_CannotUseAccountFeatures_AndIsToldToLogIn(Feature feature)
    {
        var result = Access.Check(Guest(), feature);

        Assert.False(result.Allowed);
        Assert.Equal(Access.LoginRequiredMessage, result.Reason);
    }

    // ---- Registered users: everything ----

    [Theory]
    [MemberData(nameof(BasicFeatures))]
    [MemberData(nameof(AccountFeatures))]
    public void LoggedInUser_CanUseEverything(Feature feature)
    {
        Assert.True(Access.CanUse(LoggedIn(), feature));
    }

    // ---- Signed out: nothing until the user logs in or picks guest ----

    [Theory]
    [MemberData(nameof(BasicFeatures))]
    [MemberData(nameof(AccountFeatures))]
    public void SignedOut_CannotUseAnything(Feature feature)
    {
        var result = Access.Check(SignedOut(), feature);

        Assert.False(result.Allowed);
        Assert.Equal(Access.LoginOrGuestMessage, result.Reason);
    }

    [Fact]
    public void AfterLogout_DashboardAndWatchlistAreBlocked()
    {
        var s = LoggedIn();
        s.SignOut();

        Assert.False(Access.CanUse(s, Feature.Dashboard));
        Assert.False(Access.CanUse(s, Feature.Watchlist));
    }

    [Fact]
    public void RequiresAccount_MatchesAccessRules()
    {
        Assert.True(Access.RequiresAccount(Feature.Watchlist));
        Assert.False(Access.RequiresAccount(Feature.Search));
    }
}
