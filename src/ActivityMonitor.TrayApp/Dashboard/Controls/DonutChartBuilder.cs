using System.Windows.Media;
using System.Windows.Shapes;
using Point = System.Windows.Point;
using Size = System.Windows.Size;

namespace ActivityMonitor.TrayApp.Dashboard.Controls;

/// <summary>
/// 环形图（Donut Chart）生成器。
/// 根据软件分布列表生成 WPF Path 切片集合，每片代表一个软件的占比。
/// 切片总数超过 <see cref="MaxSlices"/> 时，超出部分归入"其他"切片。
/// </summary>
public static class DonutChartBuilder
{
    private const int MaxSlices = 7;
    private const double OuterRadius = 36;
    private const double InnerRadius = 22;
    private static readonly Point Center = new(OuterRadius + 1, OuterRadius + 1);

    private static readonly System.Windows.Media.Color[] SliceColors =
    {
        System.Windows.Media.Color.FromRgb(0x00, 0x78, 0xD4),
        System.Windows.Media.Color.FromRgb(0x10, 0x7C, 0x10),
        System.Windows.Media.Color.FromRgb(0xFF, 0x8C, 0x00),
        System.Windows.Media.Color.FromRgb(0x7B, 0x3F, 0xB0),
        System.Windows.Media.Color.FromRgb(0xD1, 0x34, 0x38),
        System.Windows.Media.Color.FromRgb(0x00, 0xBC, 0xBC),
        System.Windows.Media.Color.FromRgb(0xE8, 0x11, 0x23),
        System.Windows.Media.Color.FromRgb(0x49, 0x79, 0x6B),
    };

    private static readonly System.Windows.Media.Color EmptyColor =
        System.Windows.Media.Color.FromRgb(0xE8, 0xE8, 0xE8);

    public static List<Path> BuildDonut(List<(double percentage, string name)> percentages)
    {
        var paths = new List<Path>();

        if (percentages.Count == 0 || percentages.All(p => p.percentage <= 0))
        {
            paths.Add(CreateSlice(0, 360, EmptyColor));
            return paths;
        }

        List<(double percentage, string name)> slices;
        if (percentages.Count > MaxSlices)
        {
            var top = percentages.Take(MaxSlices - 1).ToList();
            var otherPct = percentages.Skip(MaxSlices - 1).Sum(p => p.percentage);
            top.Add((otherPct, "其他"));
            slices = top;
        }
        else
        {
            slices = percentages;
        }

        slices = slices.Where(s => s.percentage >= 1).ToList();
        var currentAngle = 0.0;
        var colorIndex = 0;

        foreach (var (pct, _) in slices)
        {
            var sweepAngle = pct * 3.6;
            var color = SliceColors[colorIndex % SliceColors.Length];
            paths.Add(CreateSlice(currentAngle, sweepAngle, color));
            currentAngle += sweepAngle;
            colorIndex++;
        }

        var remaining = 360.0 - currentAngle;
        if (remaining > 1.0)
            paths.Add(CreateSlice(currentAngle, remaining, EmptyColor));

        return paths;
    }

    private static Path CreateSlice(double startAngle, double sweepAngle, System.Windows.Media.Color fill)
    {
        var startRad = startAngle * Math.PI / 180;
        var endRad = (startAngle + sweepAngle) * Math.PI / 180;
        var cx = Center.X;
        var cy = Center.Y;

        var outerStart = new Point(cx + OuterRadius * Math.Sin(startRad), cy - OuterRadius * Math.Cos(startRad));
        var outerEnd = new Point(cx + OuterRadius * Math.Sin(endRad), cy - OuterRadius * Math.Cos(endRad));
        var innerStart = new Point(cx + InnerRadius * Math.Sin(endRad), cy - InnerRadius * Math.Cos(endRad));
        var innerEnd = new Point(cx + InnerRadius * Math.Sin(startRad), cy - InnerRadius * Math.Cos(startRad));

        var isLargeArc = sweepAngle > 180;
        var outerSize = new Size(OuterRadius, OuterRadius);
        var innerSize = new Size(InnerRadius, InnerRadius);

        var figure = new PathFigure { StartPoint = outerStart, IsClosed = true };
        figure.Segments.Add(new ArcSegment(outerEnd, outerSize, 0, isLargeArc, SweepDirection.Clockwise, true));
        figure.Segments.Add(new LineSegment(innerStart, true));
        figure.Segments.Add(new ArcSegment(innerEnd, innerSize, 0, isLargeArc, SweepDirection.Counterclockwise, true));

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        geometry.Freeze();

        return new Path { Data = geometry, Fill = new SolidColorBrush(fill), SnapsToDevicePixels = true };
    }
}
