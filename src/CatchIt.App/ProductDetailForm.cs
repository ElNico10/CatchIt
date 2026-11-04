using System.Globalization;
using CatchIt.Core;
using CatchIt.Core.Pricing;
using CatchIt.Core.Search;

namespace CatchIt.App;

/// <summary>
/// The detail window for one listing (user stories #7, #8, #12, #18 and #21): its price and On Sale flag,
/// lowest and highest price, a price history chart with selectable time ranges, up to three suggested
/// alternatives, and the simulated-data notice.
/// </summary>
/// <remarks>
/// Everything is loaded once, when the window is created, from a few indexed lookups. Prices, seller ratings
/// and price history are SIMULATED demo data, and the window says so. Double-clicking an alternative opens its
/// own detail window.
/// </remarks>
public partial class ProductDetailForm : Form
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    private readonly ProductDetailService _service;
    private readonly WatchlistService _watchlist;
    private readonly ProductDetail? _detail;
    private readonly DateOnly _today;

    // Set while the code itself checks a range button, so that does not redraw the chart twice.
    private bool _settingRange;

    /// <summary>Creates the window for a listing.</summary>
    /// <param name="service">Supplies the listing's details.</param>
    /// <param name="watchlist">Used by the Add to / Remove from watchlist button.</param>
    /// <param name="listingId">The listing to show, as listed by the search screen.</param>
    public ProductDetailForm(ProductDetailService service, WatchlistService watchlist, long listingId)
    {
        _service = service;
        _watchlist = watchlist;
        _today = service.Today;
        InitializeComponent();
        lblDisclaimer.Text = Disclaimer.SimulatedData;

        _detail = service.GetDetail(listingId);
        if (_detail == null)
        {
            // The listing disappeared (for example the catalog was reloaded): say so instead of showing blanks.
            lblTitle.Text = "This listing is no longer available.";
            flowRange.Enabled = false;
            chart.Visible = false;
            btnWatch.Visible = false;
            return;
        }

        ShowListing(_detail);
        ShowWatchButton();
        ShowAlternatives(_detail.Alternatives);

        // Open on the longest range the history actually covers (12 months when there is that much).
        _settingRange = true;
        RangeButton(PriceChartMath.DefaultRange(_detail.History, _today)).Checked = true;
        _settingRange = false;
        ShowChart();
    }

    // ---- The listing ----

    private void ShowListing(ProductDetail d)
    {
        var listing = d.Listing;
        lblTitle.Text = listing.ProductName;
        tips.SetToolTip(lblTitle, listing.ProductName); // the full name, since long titles are cut to two lines

        var subtitle = $"{listing.BrandName}  ·  {Display.TierName(listing.QualityTier)} brand  ·  {listing.MarketplaceName}";
        lblSubtitle.Text = string.IsNullOrEmpty(d.Category) ? subtitle : $"{subtitle}  ·  {d.Category}";
        tips.SetToolTip(lblSubtitle, lblSubtitle.Text);

        // A listing with no price says so, rather than showing a misleading number (story #6).
        lblPrice.Text = listing.CurrentPrice is { } price ? Display.Money(price) : "Price unavailable";
        lblPrice.Font = listing.CurrentPrice == null ? new Font(lblPrice.Font.FontFamily, 14F, FontStyle.Bold) : lblPrice.Font;
        lblOnSale.Visible = listing.OnSale;
        tips.SetToolTip(lblOnSale, $"At least {SaleCheck.MinGap:P0} below its {SaleCheck.AverageDays}-day average price.");

        var lines = new List<string>
        {
            d.Extremes is { } x ? $"Lowest price: {Display.Money(x.LowPrice)} ({Display.Date(x.LowDate)})" : "Lowest price: not recorded",
            d.Extremes is { } y ? $"Highest price: {Display.Money(y.HighPrice)} ({Display.Date(y.HighDate)})" : "Highest price: not recorded",
            d.Average90 is { } avg ? $"{SaleCheck.AverageDays}-day average: {Display.Money(avg)}" : $"{SaleCheck.AverageDays}-day average: not enough history",
            d.ProductRating is { } r
                ? $"Product rating: {r.ToString("0.0", Invariant)} / 5" + (d.RatingCount is { } n ? $" ({n:N0} ratings)" : "")
                : "Product rating: not available",
            $"Seller rating (simulated): {listing.SellerRating.ToString("0.0", Invariant)} / 5",
        };
        lblFacts.Text = string.Join(Environment.NewLine + Environment.NewLine, lines);
    }

    // ---- The watchlist ----

    /// <summary>Makes the button say what a click will do. Guests see "Add" too; clicking it explains they need to log in.</summary>
    private void ShowWatchButton() =>
        btnWatch.Text = _watchlist.IsWatching(_detail!.Listing.ProductId) ? "Remove from watchlist" : "Add to watchlist";

    private void btnWatch_Click(object? sender, EventArgs e)
    {
        var productId = _detail!.Listing.ProductId;
        var result = _watchlist.IsWatching(productId) ? _watchlist.Remove(productId) : _watchlist.Add(productId);
        lblWatchStatus.ForeColor = result.Success ? Color.ForestGreen : Color.Firebrick;
        lblWatchStatus.Text = result.Message;
        ShowWatchButton();
    }

    // ---- The chart ----

    private RadioButton RangeButton(ChartRange range) => range switch
    {
        ChartRange.ThreeMonths => rbThreeMonths,
        ChartRange.SixMonths => rbSixMonths,
        ChartRange.TwelveMonths => rbTwelveMonths,
        _ => rbAll,
    };

    private ChartRange SelectedRange() =>
        Enum.Parse<ChartRange>((string)flowRange.Controls.OfType<RadioButton>().First(b => b.Checked).Tag!);

    /// <summary>A range button was checked: redraw the chart for that period only.</summary>
    private void range_CheckedChanged(object? sender, EventArgs e)
    {
        if (_settingRange || sender is not RadioButton { Checked: true }) return;
        ShowChart();
    }

    private void ShowChart()
    {
        if (_detail == null) return;
        chart.SetData(PriceChartMath.Filter(_detail.History, SelectedRange(), _today));
    }

    // ---- The alternatives ----

    private void ShowAlternatives(IReadOnlyList<AlternativeSuggestion> alternatives)
    {
        flowAlternatives.Controls.Clear();
        lblNoAlternatives.Visible = alternatives.Count == 0;
        flowAlternatives.Visible = alternatives.Count > 0;
        foreach (var alt in alternatives)
            flowAlternatives.Controls.Add(BuildCard(alt));
    }

    /// <summary>One alternative as a bordered card: why it is suggested, its name, brand, price and ratings.</summary>
    private Panel BuildCard(AlternativeSuggestion alt)
    {
        var card = new Panel
        {
            Size = new Size(290, 176),
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(0, 0, 12, 0),
            Cursor = Cursors.Hand,
        };

        // One line (or two, when it wraps) per reason, each in its own colour, stacked from the top.
        var y = 6;
        foreach (var tag in alt.Tags)
        {
            var reason = new Label
            {
                Text = tag,
                Font = new Font(Font, FontStyle.Bold),
                ForeColor = TagColor(tag),
                AutoSize = true,
                MaximumSize = new Size(272, 0),
                Location = new Point(8, y),
            };
            card.Controls.Add(reason);
            y = reason.Bottom + 2;
        }
        var name = new Label
        {
            Text = alt.ProductName,
            AutoEllipsis = true,
            Location = new Point(8, y + 2),
            Size = new Size(272, 36),
        };
        var price = new Label
        {
            Text = Display.Money(alt.Price),
            Font = new Font(Font.FontFamily, 13F, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(8, 136),
        };
        var detail = new Label
        {
            Text = $"{alt.BrandName} · {Display.TierName(alt.QualityTier)} · {alt.MarketplaceName}"
                   + (alt.Rating is { } r ? $" · {r.ToString("0.0", Invariant)}★" : ""),
            AutoEllipsis = true,
            ForeColor = SystemColors.GrayText,
            Location = new Point(8, 118),
            Size = new Size(272, 18),
        };
        tips.SetToolTip(card, $"{alt.ProductName}\nDouble-click to open it.");

        card.Controls.AddRange(new Control[] { name, detail, price });

        // A double-click anywhere on the card (its labels cover most of it) opens that alternative.
        void Open(object? s, EventArgs e) => OpenAlternative(alt);
        card.DoubleClick += Open;
        foreach (Control child in card.Controls) child.DoubleClick += Open;
        return card;
    }

    /// <summary>The colour of a card's first reason, so the three kinds are easy to tell apart.</summary>
    private static Color TagColor(string tag) =>
        tag.StartsWith("Cheapest") ? Color.ForestGreen
        : tag.StartsWith("Better") ? Color.RoyalBlue
        : Color.DarkOrange;

    private void OpenAlternative(AlternativeSuggestion alt)
    {
        using var window = new ProductDetailForm(_service, _watchlist, alt.ListingId);
        window.ShowDialog(this);
    }
}
