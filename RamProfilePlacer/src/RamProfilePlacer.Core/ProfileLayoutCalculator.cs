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

        var placements = new List<ProfilePlacement>();

        // --- Bottom (только для окон) ---
        if (!isDoor)
        {
            // Начало: BottomLeft, направление: к BottomRight, длина: ширина
            var dir = NormalizedDirection(dims.BottomLeft3D, dims.BottomRight3D);
            placements.Add(new ProfilePlacement(
                OpeningSide.Bottom,
                dims.BottomLeft3D,
                dir,
                dims.Width));
        }

        // --- Top ---
        {
            // Начало: TopLeft, направление: к TopRight, длина: ширина
            var dir = NormalizedDirection(dims.TopLeft3D, dims.TopRight3D);
            placements.Add(new ProfilePlacement(
                OpeningSide.Top,
                dims.TopLeft3D,
                dir,
                dims.Width));
        }

        // --- Left ---
        {
            // Начало: BottomLeft, направление: к TopLeft, длина: высота
            var dir = NormalizedDirection(dims.BottomLeft3D, dims.TopLeft3D);
            placements.Add(new ProfilePlacement(
                OpeningSide.Left,
                dims.BottomLeft3D,
                dir,
                dims.Height));
        }

        // --- Right ---
        {
            // Начало: BottomRight, направление: к TopRight, длина: высота
            var dir = NormalizedDirection(dims.BottomRight3D, dims.TopRight3D);
            placements.Add(new ProfilePlacement(
                OpeningSide.Right,
                dims.BottomRight3D,
                dir,
                dims.Height));
        }

        return placements;
    }

    private static Point3D NormalizedDirection(Point3D from, Point3D to)
    {
        var diff = to - from;
        return diff.Normalize();
    }
}
