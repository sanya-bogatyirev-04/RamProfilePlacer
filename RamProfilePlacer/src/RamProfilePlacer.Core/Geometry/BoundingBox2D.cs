namespace RamProfilePlacer.Core.Geometry;

/// <summary>
/// Ограничивающий прямоугольник в плоскости грани (оси U/V).
/// Используется для определения внешнего/внутренних контуров EdgeLoops.
/// </summary>
public sealed class BoundingBox2D
{
    public double MinU { get; }
    public double MaxU { get; }
    public double MinV { get; }
    public double MaxV { get; }

    public double Width => MaxU - MinU;
    public double Height => MaxV - MinV;
    public double Area => Width * Height;

    public Point2D Center => new((MinU + MaxU) / 2.0, (MinV + MaxV) / 2.0);

    public BoundingBox2D(double minU, double maxU, double minV, double maxV)
    {
        if (maxU < minU) throw new ArgumentException("maxU must be >= minU");
        if (maxV < minV) throw new ArgumentException("maxV must be >= minV");
        MinU = minU;
        MaxU = maxU;
        MinV = minV;
        MaxV = maxV;
    }

    /// <summary>
    /// Создаёт BoundingBox2D из набора точек.
    /// </summary>
    public static BoundingBox2D FromPoints(IEnumerable<Point2D> points)
    {
        var list = points.ToList();
        if (list.Count < 2)
            throw new ArgumentException("At least 2 points required to build BoundingBox2D.");

        double minU = list[0].U, maxU = list[0].U;
        double minV = list[0].V, maxV = list[0].V;

        foreach (var p in list.Skip(1))
        {
            if (p.U < minU) minU = p.U;
            if (p.U > maxU) maxU = p.U;
            if (p.V < minV) minV = p.V;
            if (p.V > maxV) maxV = p.V;
        }

        return new BoundingBox2D(minU, maxU, minV, maxV);
    }

    /// <summary>
    /// Проверяет, попадает ли точка внутрь прямоугольника (включая границы + tolerance).
    /// </summary>
    public bool Contains(Point2D point, double tolerance = 1e-6)
    {
        return point.U >= MinU - tolerance && point.U <= MaxU + tolerance &&
               point.V >= MinV - tolerance && point.V <= MaxV + tolerance;
    }

    /// <summary>
    /// Угловые точки прямоугольника: BottomLeft, BottomRight, TopRight, TopLeft.
    /// </summary>
    public (Point2D BottomLeft, Point2D BottomRight, Point2D TopRight, Point2D TopLeft) GetCorners()
    {
        return (
            new Point2D(MinU, MinV),  // BottomLeft
            new Point2D(MaxU, MinV),  // BottomRight
            new Point2D(MaxU, MaxV),  // TopRight
            new Point2D(MinU, MaxV)   // TopLeft
        );
    }

    public override string ToString() =>
        $"BBox2D U=[{MinU:F4},{MaxU:F4}] V=[{MinV:F4},{MaxV:F4}] W={Width:F4} H={Height:F4} A={Area:F4}";
}
