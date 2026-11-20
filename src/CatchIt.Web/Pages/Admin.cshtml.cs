using CatchIt.Core.Auth;
using CatchIt.Core.Pricing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CatchIt.Web.Pages;

/// <summary>The administrator's price refresh (issue #19). Everyone else sees why they cannot use it.</summary>
[Authorize]
public class AdminModel(Session session, PriceRefreshService refresh) : PageModel
{
    /// <summary>True when the logged-in user is an administrator.</summary>
    public bool Allowed => Access.Check(session, Feature.PriceRefresh).Allowed;

    /// <summary>Why access was refused, when it was.</summary>
    public string? Denied => Access.Check(session, Feature.PriceRefresh).Reason;

    /// <summary>The most recent refresh, or null if prices were never refreshed.</summary>
    public RefreshLogEntry? Last { get; private set; }

    /// <summary>What the refresh just did, or why it failed.</summary>
    public string? Result { get; private set; }

    /// <summary>True when <see cref="Result"/> is an error.</summary>
    public bool ResultIsError { get; private set; }

    /// <summary>Shows the last refresh.</summary>
    public void OnGet() => Last = Allowed ? refresh.LastRefresh() : null;

    /// <summary>Runs the refresh. On the full catalog this takes a while, and the page waits for it.</summary>
    public IActionResult OnPostRefresh()
    {
        try
        {
            var outcome = refresh.Refresh();
            Result = outcome.Describe();
            ResultIsError = !outcome.Success;
        }
        catch (Exception ex)
        {
            Result = $"The refresh failed and no prices were changed: {ex.Message}";
            ResultIsError = true;
        }
        Last = Allowed ? refresh.LastRefresh() : null;
        return Page();
    }
}
