using CatchIt.Core.Search;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CatchIt.Web.Pages;

/// <summary>The logged-in user's watchlist (user stories #13 and #14): the lowest current price of each watched product, with removal and target prices.</summary>
[Authorize]
public class WatchlistModel(WatchlistService watchlist) : PageModel
{
    /// <summary>The target price typed for one product.</summary>
    [BindProperty] public double? TargetPrice { get; set; }

    /// <summary>The watched products.</summary>
    public IReadOnlyList<WatchedProduct> Items { get; private set; } = [];

    /// <summary>The result of the last action.</summary>
    public string? Message { get; private set; }

    /// <summary>True when <see cref="Message"/> is an error.</summary>
    public bool MessageIsError { get; private set; }

    /// <summary>Shows the list.</summary>
    public void OnGet() => Items = watchlist.List();

    /// <summary>Removes a product.</summary>
    public IActionResult OnPostRemove(long productId) => Act(watchlist.Remove(productId));

    /// <summary>Sets or clears a product's target price (an empty box clears it).</summary>
    public IActionResult OnPostTarget(long productId) => Act(watchlist.SetTargetPrice(productId, TargetPrice));

    private IActionResult Act(WatchlistResult result)
    {
        Message = result.Message;
        MessageIsError = !result.Success;
        Items = watchlist.List();
        return Page();
    }
}
