namespace CatchIt.Core.Auth;

public enum Feature
{
    // Basic features: available to guests and registered users.
    Search,
    PriceDisplay,
    PriceHistory,
    SaleFlags,
    BrandFilter,
    SellerFilter,
    Alternatives,

    // Account features: they need somewhere to save, so they require a login.
    Watchlist,
    TargetPrice,
    Dashboard,
    Profile,
}

public record AccessResult(bool Allowed, string? Reason)
{
    public static AccessResult Yes() => new(true, null);
    public static AccessResult No(string reason) => new(false, reason);
}

/// <summary>
/// Single place that decides who can use what. Forms should ask this instead of
/// hard-coding their own rules.
/// </summary>
public static class Access
{
    public const string LoginOrGuestMessage = "Please log in or continue as a guest";
    public const string LoginRequiredMessage = "Please log in to use this feature. Guests can't save data.";

    private static readonly HashSet<Feature> AccountOnly = new()
    {
        Feature.Watchlist,
        Feature.TargetPrice,
        Feature.Dashboard,
        Feature.Profile,
    };

    public static bool RequiresAccount(Feature feature) => AccountOnly.Contains(feature);

    public static AccessResult Check(Session session, Feature feature)
    {
        if (!session.IsActive)
            return AccessResult.No(LoginOrGuestMessage);

        if (RequiresAccount(feature) && !session.IsLoggedIn)
            return AccessResult.No(LoginRequiredMessage);

        return AccessResult.Yes();
    }

    public static bool CanUse(Session session, Feature feature) => Check(session, feature).Allowed;
}
