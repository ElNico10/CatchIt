using System.Drawing.Drawing2D;
using System.Globalization;
using CatchIt.Core.Pricing;

namespace CatchIt.App;

/// <summary>
/// A small line chart of a listing's price over time, drawn directly with GDI+ (no charting package).
/// It labels both axes with round numbers, marks the lowest and highest price shown, and shows the
/// date and price of the point nearest the mouse.
/// </summary>
/// <remarks>
/// Give it points with <see cref="SetData"/>. When there are too few points to chart (less than a week of
/// history, user story #7) it shows <see cref="PriceChartMath.NotEnoughHistoryMessage"/> instead. All the
/// calculations that can be tested live in <see cref="PriceChartMath"/>; this class only draws.
/// </remarks>
public class PriceChart : Control
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    // Space around the plot area, in pixels: room for the price labels on the left and the dates underneath.
    private const int LeftMargin = 62, RightMargin = 24, TopMargin = 22, BottomMargin = 30;

    private IReadOnlyList<PricePoint> _points = Array.Empty<PricePoint>();
    private int _hover = -1;

    /// <summary>Creates an empty chart.</summary>
    public PriceChart()
    {
        // Double buffering stops the flicker that redrawing on every mouse move would otherwise cause.
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint
                 | ControlStyles.ResizeRedraw, true);
        BackColor = SystemColors.Window;
        ForeColor = SystemColors.ControlText;
        AccessibleName = "Price history chart";
        AccessibleRole = AccessibleRole.Chart;
    }

    /// <summary>Whether the current points are enough to draw a chart.</summary>
    public bool HasChart => PriceChartMath.HasEnoughHistory(_points);

    /// <summary>The index (into the points last given) of the point the mouse is nearest, or -1 for none.</summary>
    public int HoverIndex => _hover;

    /// <summary>Replaces the points being charted.</summary>
    /// <param name="points">The prices to draw, oldest first.</param>
    public void SetData(IReadOnlyList<PricePoint> points)
    {
        _points = points;
        _hover = -1;
        Invalidate();
    }

    /// <summary>Finds the point nearest a horizontal position, for the hover display.</summary>
    /// <param name="x">A position in the control's own coordinates.</param>
    /// <returns>The index of the nearest point, or -1 when there is no chart or the position is outside the plot.</returns>
    public int NearestIndex(int x)
    {
        if (!HasChart) return -1;
        var plot = PlotArea();
        if (x < plot.Left - 6 || x > plot.Right + 6) return -1;

        var best = 0;
        var bestDistance = double.MaxValue;
        for (var i = 0; i < _points.Count; i++)
        {
            var distance = Math.Abs(XOf(_points[i].Date, plot) - x);
            if (distance < bestDistance) { best = i; bestDistance = distance; }
        }
        return best;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var nearest = NearestIndex(e.X);
        if (nearest == _hover) return;
        _hover = nearest;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hover < 0) return;
        _hover = -1;
        Invalidate();
    }

    // ---- Drawing ----

    private Rectangle PlotArea() =>
        new(LeftMargin, TopMargin, Math.Max(10, Width - LeftMargin - RightMargin), Math.Max(10, Height - TopMargin - BottomMargin));

    private float XOf(DateOnly date, Rectangle plot)
    {
        var first = _points[0].Date.DayNumber;
        var span = Math.Max(1, _points[^1].Date.DayNumber - first);
        return plot.Left + (float)(date.DayNumber - first) / span * plot.Width;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.Clear(BackColor);

        if (!HasChart)
        {
            using var brush = new SolidBrush(SystemColors.GrayText);
            using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(PriceChartMath.NotEnoughHistoryMessage, Font, brush, ClientRectangle, format);
            return;
        }

        g.SmoothingMode = SmoothingMode.AntiAlias;
        var plot = PlotArea();

        var ticks = PriceChartMath.NiceTicks(_points.Min(p => p.Price), _points.Max(p => p.Price));
        var axisMin = ticks[0];
        var axisMax = ticks[^1];
        float YOf(double price) => plot.Bottom - (float)((price - axisMin) / (axisMax - axisMin)) * plot.Height;

        DrawAxes(g, plot, ticks, YOf);

        var line = _points.Select(p => new PointF(XOf(p.Date, plot), YOf(p.Price))).ToArray();
        using (var pen = new Pen(SystemColors.Highlight, 2f) { LineJoin = LineJoin.Round })
            g.DrawLines(pen, line);

        // The lowest and highest price on screen (the first of equal ones), so a bargain or a peak stands out.
        var lowIndex = IndexOf(_points, minimum: true);
        var highIndex = IndexOf(_points, minimum: false);
        DrawExtreme(g, line[lowIndex], _points[lowIndex].Price, "Low", Color.ForestGreen, below: true, plot);
        if (highIndex != lowIndex)
            DrawExtreme(g, line[highIndex], _points[highIndex].Price, "High", Color.Firebrick, below: false, plot);

        if (_hover >= 0) DrawHover(g, line[_hover], _points[_hover], plot);
    }

    private static int IndexOf(IReadOnlyList<PricePoint> points, bool minimum)
    {
        var best = 0;
        for (var i = 1; i < points.Count; i++)
            if (minimum ? points[i].Price < points[best].Price : points[i].Price > points[best].Price) best = i;
        return best;
    }

    private void DrawAxes(Graphics g, Rectangle plot, IReadOnlyList<double> ticks, Func<double, float> yOf)
    {
        using var gridPen = new Pen(Color.FromArgb(60, SystemColors.GrayText));
        using var labelBrush = new SolidBrush(SystemColors.GrayText);
        var wholeDollars = ticks.Count < 2 || ticks[1] - ticks[0] >= 1;

        foreach (var tick in ticks)
        {
            var y = yOf(tick);
            g.DrawLine(gridPen, plot.Left, y, plot.Right, y);
            var text = "$" + tick.ToString(wholeDollars ? "N0" : "N2", Invariant);
            var size = g.MeasureString(text, Font);
            g.DrawString(text, Font, labelBrush, plot.Left - size.Width - 4, y - size.Height / 2);
        }

        // About five evenly spaced dates along the bottom; months and year when the chart spans most of a year.
        var span = _points[^1].Date.DayNumber - _points[0].Date.DayNumber;
        var format = span > 200 ? "MMM yyyy" : "MMM d";
        const int labels = 5;
        for (var i = 0; i < labels; i++)
        {
            var date = DateOnly.FromDayNumber(_points[0].Date.DayNumber + span * i / (labels - 1));
            var text = date.ToString(format, Invariant);
            var size = g.MeasureString(text, Font);
            var x = Math.Clamp(XOf(date, plot) - size.Width / 2, 0, Width - size.Width);
            g.DrawString(text, Font, labelBrush, x, plot.Bottom + 6);
        }
    }

    private void DrawExtreme(Graphics g, PointF at, double price, string label, Color color, bool below, Rectangle plot)
    {
        using var brush = new SolidBrush(color);
        g.FillEllipse(brush, at.X - 4, at.Y - 4, 8, 8);

        using var bold = new Font(Font, FontStyle.Bold);
        var text = $"{label} ${price.ToString("N2", Invariant)}";
        var size = g.MeasureString(text, bold);
        var x = Math.Clamp(at.X - size.Width / 2, plot.Left, Width - size.Width - 2);
        var y = below ? at.Y + 7 : at.Y - 7 - size.Height;
        g.DrawString(text, bold, brush, x, Math.Clamp(y, 0, Height - size.Height));
    }

    private void DrawHover(Graphics g, PointF at, PricePoint point, Rectangle plot)
    {
        using (var guide = new Pen(SystemColors.GrayText) { DashStyle = DashStyle.Dash })
            g.DrawLine(guide, at.X, plot.Top, at.X, plot.Bottom);
        g.FillEllipse(SystemBrushes.Window, at.X - 5, at.Y - 5, 10, 10);
        using (var ring = new Pen(SystemColors.Highlight, 2f))
            g.DrawEllipse(ring, at.X - 5, at.Y - 5, 10, 10);

        var text = $"{point.Date.ToString("MMM d, yyyy", Invariant)}\n${point.Price.ToString("N2", Invariant)}";
        var size = g.MeasureString(text, Font);
        var box = new RectangleF(at.X + 12, at.Y - size.Height - 12, size.Width + 10, size.Height + 6);
        // Keep the box on the chart: flip to the left of the point near the right edge, and below it near the top.
        if (box.Right > Width - 2) box.X = at.X - 12 - box.Width;
        if (box.Top < 2) box.Y = at.Y + 12;

        g.FillRectangle(SystemBrushes.Info, box);
        g.DrawRectangle(SystemPens.ControlDark, box.X, box.Y, box.Width, box.Height);
        g.DrawString(text, Font, SystemBrushes.InfoText, box.X + 5, box.Y + 3);
    }
}
