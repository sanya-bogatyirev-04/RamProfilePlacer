namespace RamProfilePlacer.Core.Geometry;

/// <summary>
/// Трёхмерная точка без зависимости от Revit API.
/// </summary>
public readonly struct Point3D(double x, double y, double z)
{
    public double X { get; } = x;
    public double Y { get; } = y;
    public double Z { get; } = z;

    public static Point3D operator +(Point3D a, Point3D b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Point3D operator -(Point3D a, Point3D b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Point3D operator *(Point3D p, double s) => new(p.X * s, p.Y * s, p.Z * s);
    public static Point3D operator *(double s, Point3D p) => p * s;

    public double DotProduct(Point3D other) => X * other.X + Y * other.Y + Z * other.Z;

    public Point3D CrossProduct(Point3D other) => new(
        Y * other.Z - Z * other.Y,
        Z * other.X - X * other.Z,
        X * other.Y - Y * other.X);

    public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);

    public Point3D Normalize()
    {
        var len = Length;
        if (len < 1e-12) throw new InvalidOperationException("Cannot normalize zero-length vector.");
        return new(X / len, Y / len, Z / len);
    }

    public Point3D Negate() => new(-X, -Y, -Z);

    /// <summary>
    /// Явная проекция точки this на плоскость заданную origin и нормалью normal.
    /// Используется вместо Face.Project() так как тот не работает над отверстиями.
    /// </summary>
    public Point3D ProjectOnPlane(Point3D planeOrigin, Point3D planeNormal)
    {
        var diff = this - planeOrigin;
        var dist = diff.DotProduct(planeNormal);
        return this - planeNormal * dist;
    }

    public override string ToString() => $"({X:F6}, {Y:F6}, {Z:F6})";
}
