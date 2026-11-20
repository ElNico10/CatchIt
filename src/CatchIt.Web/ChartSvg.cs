using System.Globalization;
using System.Text;
using CatchIt.Core.Pricing;

namespace CatchIt.Web;

/// <summary>Draws the price history as an inline SVG line chart, using the same axis maths as the desktop chart.</summary>
public static class ChartSvg
{
    private const int W = 640, H = 280, Left = 64, Right = 16, Top = 16, Bottom = 36;

    private static string N(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>Returns the SVG markup for the points, oldest first. The caller checks there is enough history.</summary>
    public static string Render(IReadOnlyList<PricePoint> points)
    {
        var ticks = PriceChartMath.NiceTicks(points.Min(p => p.Price), points.Max(p => p.Price));
        double lo = ticks[0], hi = ticks[^1];
        var first = points[0].Date.DayNumber;
        var span = Math.Max(1, points[^1].Date.DayNumber - first);

        double X(DateOnly d) => Left + (d.DayNumber - first) * (W - Left - Right) / (double)span;
        double Y(double price) => Top + (hi - price) * (H - Top - Bottom) / (hi - lo);

        var svg = new StringBuilder();
        svg.Append($"<svg id=\"chart\" viewBox=\"0 0 {W} {H}\" role=\"img\" aria-label=\"Price history\" style=\"max-width:100%;height:auto\">");
        foreach (var t in ticks)
        {
            svg.Append($"<line x1=\"{Left}\" x2=\"{W - Right}\" y1=\"{N(Y(t))}\" y2=\"{N(Y(t))}\" stroke=\"#e3e3e3\" />");
            svg.Append($"<text x=\"{Left - 6}\" y=\"{N(Y(t) + 4)}\" text-anchor=\"end\" font-size=\"11\" fill=\"#555\">{Format.Money(t)}</text>");
        }
        svg.Append($"<text x=\"{Left}\" y=\"{H - 12}\" font-size=\"11\" fill=\"#555\">{Format.Date(points[0].Date)}</text>");
        svg.Append($"<text x=\"{W - Right}\" y=\"{H - 12}\" text-anchor=\"end\" font-size=\"11\" fill=\"#555\">{Format.Date(points[^1].Date)}</text>");
        svg.Append("<polyline fill=\"none\" stroke=\"#1a5fb4\" stroke-width=\"2\" points=\"");
        svg.Append(string.Join(' ', points.Select(p => $"{N(X(p.Date))},{N(Y(p.Price))}")));
        svg.Append("\" /></svg>");
        return svg.ToString();
    }
}
