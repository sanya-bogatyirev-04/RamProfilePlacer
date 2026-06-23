namespace RamProfilePlacer.Core.Geometry;

/// <summary>
/// Двумерная точка в плоскости грани стены (в осях U/V грани).
/// </summary>
public readonly struct Point2D(double u, double v)
{
    public double U { get; } = u;
    public double V { get; } = v;

    public static Point2D operator +(Point2D a, Point2D b) => new(a.U + b.U, a.V + b.V);
    public static Point2D operator -(Point2D a, Point2D b) => new(a.U - b.U, a.V - b.V);
    public static Point2D operator *(Point2D p, double s) => new(p.U * s, p.V * s);

    public double DistanceTo(Point2D other)
    {
        var du = U - other.U;
        var dv = V - other.V;
        return Math.Sqrt(du * du + dv * dv);
    }

    public override string ToString() => $"({U:F6}, {V:F6})";
}
