using CatchIt.Core.Search;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CatchIt.Web.Pages;

/// <summary>The search page (user stories #5, #6, #9, #10, #11): open to everyone, guests included.</summary>
public class IndexModel(ListingSearchService search) : PageModel
{
    /// <summary>The text the visitor typed.</summary>
    [BindProperty(SupportsGet = true)]
    public string? Q { get; set; }

    /// <summary>Only brands of this quality tier (1 Budget, 2 Standard, 3 Premium), or null for any.</summary>
    [BindProperty(SupportsGet = true)]
    public int? Tier { get; set; }

    /// <summary>Only sellers rated at least this, or null for any.</summary>
    [BindProperty(SupportsGet = true)]
    public double? MinRating { get; set; }

    /// <summary>The results, or null before the first search.</summary>
    public ListingSearchPage? Result { get; private set; }

    /// <summary>Runs the exact search when text was submitted.</summary>
    public void OnGet()
    {
        if (!string.IsNullOrWhiteSpace(Q))
            Result = search.Search(Q, new ListingFilters(Tier, MinRating));
    }
}
