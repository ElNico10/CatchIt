using CatchIt.Core.Search;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CatchIt.Web.Pages;

/// <summary>The search page (user stories #5, #6, #9): open to everyone, guests included.</summary>
public class IndexModel(ListingSearchService search) : PageModel
{
    /// <summary>The text the visitor typed.</summary>
    [BindProperty(SupportsGet = true)]
    public string? Q { get; set; }

    /// <summary>The results, or null before the first search.</summary>
    public ListingSearchPage? Result { get; private set; }

    /// <summary>Runs the exact search when text was submitted.</summary>
    public void OnGet()
    {
        if (!string.IsNullOrWhiteSpace(Q))
            Result = search.Search(Q);
    }
}
