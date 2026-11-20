using CatchIt.Core.Pricing;
using CatchIt.Core.Search;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using CatchIt.Core.Auth;

namespace CatchIt.Web.Pages;

/// <summary>
/// One listing in detail (user stories #7, #8, #12, #13, #14): price, sale flag, lowest and highest price, 90-day
/// average, the price history chart, suggested alternatives, and the watchlist buttons for logged-in users.
/// </summary>
public class ProductModel(ProductDetailService details, WatchlistService watchlist, Session session) : PageModel
{
    /// <summary>The listing to show.</summary>
    [BindProperty(SupportsGet = true)] public long Id { get; set; }

    /// <summary>The chart range: 3m, 6m, 12m or all. Empty picks the longest range the history covers.</summary>
    [BindProperty(SupportsGet = true)] public string? Range { get; set; }

    /// <summary>The target price typed into the form. Empty removes the target.</summary>
    [BindProperty] public double? TargetPrice { get; set; }

    /// <summary>The listing and its history, or null when there is no such listing.</summary>
    public ProductDetail? Detail { get; private set; }

    /// <summary>True when the logged-in user watches this product.</summary>
    public bool IsWatching { get; private set; }

    /// <summary>The user's target price for this product, if set.</summary>
    public double? Target { get; private set; }

    /// <summary>The chart as SVG, or null when the history is too short to draw.</summary>
    public string? Chart { get; private set; }

    /// <summary>The range the chart shows.</summary>
    public ChartRange SelectedRange { get; private set; }

    /// <summary>The result of a watchlist action.</summary>
    public string? Message { get; private set; }

    /// <summary>True when <see cref="Message"/> is an error.</summary>
    public bool MessageIsError { get; private set; }

    /// <summary>True when somebody is logged in (guests cannot use the watchlist).</summary>
    public bool CanWatch => session.IsLoggedIn;

    /// <summary>The query-string value for a range.</summary>
    public static string Key(ChartRange r) => r switch { ChartRange.ThreeMonths => "3m", ChartRange.SixMonths => "6m", ChartRange.TwelveMonths => "12m", _ => "all" };

    /// <summary>Label for a range.</summary>
    public static string Label(ChartRange r) => r switch { ChartRange.ThreeMonths => "3 months", ChartRange.SixMonths => "6 months", ChartRange.TwelveMonths => "12 months", _ => "All" };

    /// <summary>Shows the page.</summary>
    public IActionResult OnGet() => Load();

    /// <summary>The "Add to watchlist" button.</summary>
    public IActionResult OnPostWatch() => Act(() => watchlist.Add(ProductId()));

    /// <summary>The "Remove from watchlist" button.</summary>
    public IActionResult OnPostUnwatch() => Act(() => watchlist.Remove(ProductId()));

    /// <summary>The "Save target" button; an empty box removes the target.</summary>
    public IActionResult OnPostTarget() => Act(() => watchlist.SetTargetPrice(ProductId(), TargetPrice));

    private long ProductId() => details.GetDetail(Id)?.Listing.ProductId ?? 0;

    private IActionResult Act(Func<WatchlistResult> action)
    {
        var result = action();
        var page = Load();
        Message = result.Message;
        MessageIsError = !result.Success;
        return page;
    }

    private IActionResult Load()
    {
        Detail = details.GetDetail(Id);
        if (Detail == null)
            return NotFound();

        if (session.IsLoggedIn)
        {
            var item = watchlist.List().FirstOrDefault(w => w.ProductId == Detail.Listing.ProductId);
            IsWatching = item != null;
            Target = item?.TargetPrice;
        }

        var asOf = details.Today;
        SelectedRange = Range switch
        {
            "3m" => ChartRange.ThreeMonths,
            "6m" => ChartRange.SixMonths,
            "12m" => ChartRange.TwelveMonths,
            "all" => ChartRange.All,
            _ => PriceChartMath.DefaultRange(Detail.History, asOf),
        };
        var points = PriceChartMath.Filter(Detail.History, SelectedRange, asOf);
        Chart = PriceChartMath.HasEnoughHistory(points) ? ChartSvg.Render(points) : null;
        return Page();
    }
}
