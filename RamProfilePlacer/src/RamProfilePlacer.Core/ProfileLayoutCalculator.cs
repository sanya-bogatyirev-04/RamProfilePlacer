using RamProfilePlacer.Core.Geometry;
using RamProfilePlacer.Core.Model;

namespace RamProfilePlacer.Core;

/// <summary>
/// Вычисляет раскладку профилей (3 или 4 штуки) по периметру проёма.
/// Не зависит от Revit API.
/// </summary>
public static class ProfileLayoutCalculator
{
    /// <summary>
    /// Возвращает список размещений профилей для проёма.
    /// </summary>
    /// <param name="dims">Размеры и угловые точки проёма.</param>
    /// <param name="isDoor">true — дверь (без нижнего профиля), false — окно (4 профиля).</param>
    /// <returns>Список ProfilePlacement (3 или 4 элемента).</returns>
    public static IReadOnlyList<ProfilePlacement> Calculate(OpeningDimensions dims, bool isDoor)
    {
        ArgumentNullException.ThrowIfNull(dims);

        // Центр проёма — для направления refDir «внутрь»
        var center = Midpoint(
            Midpoint(dims.BottomLeft3D, dims.BottomRight3D),
            Midpoint(dims.TopLeft3D, dims.TopRight3D));

        var placements = new List<ProfilePlacement>();

        // --- Bottom (только для окон) ---
        if (!isDoor)
        {
            var mid = Midpoint(dims.BottomLeft3D, dims.BottomRight3D);
            var dir = NormalizedDirection(dims.BottomLeft3D, dims.BottomRight3D);
            var refDir = NormalizedDirection(mid, center);
            placements.Add(new ProfilePlacement(
                OpeningSide.Bottom, mid, dir, refDir, dims.Width));
        }

        // --- Top ---
        {
            var mid = Midpoint(dims.TopLeft3D, dims.TopRight3D);
            var dir = NormalizedDirection(dims.TopLeft3D, dims.TopRight3D);
            var refDir = NormalizedDirection(mid, center);
            placements.Add(new ProfilePlacement(
                OpeningSide.Top, mid, dir, refDir, dims.Width));
        }

        // --- Left ---
        {
            var mid = Midpoint(dims.BottomLeft3D, dims.TopLeft3D);
            var dir = NormalizedDirection(dims.BottomLeft3D, dims.TopLeft3D);
            var refDir = NormalizedDirection(mid, center);
            placements.Add(new ProfilePlacement(
                OpeningSide.Left, mid, dir, refDir, dims.Height));
        }

        // --- Right ---
        {
            var mid = Midpoint(dims.BottomRight3D, dims.TopRight3D);
            var dir = NormalizedDirection(dims.BottomRight3D, dims.TopRight3D);
            var refDir = NormalizedDirection(mid, center);
            placements.Add(new ProfilePlacement(
                OpeningSide.Right, mid, dir, refDir, dims.Height));
        }

        return placements;
    }

    private static Point3D Midpoint(Point3D a, Point3D b) =>
        new((a.X + b.X) / 2.0, (a.Y + b.Y) / 2.0, (a.Z + b.Z) / 2.0);

    private static Point3D NormalizedDirection(Point3D from, Point3D to)
    {
        var diff = to - from;
        return diff.Normalize();
    }
}
