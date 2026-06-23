using RamProfilePlacer.Core.Geometry;

namespace RamProfilePlacer.Core.Model;

/// <summary>
/// Размеры и геометрия внутреннего проёма, вычисленные из EdgeLoops или запасного пути.
/// </summary>
public sealed class OpeningDimensions
{
    /// <summary>Ширина внутреннего проёма (горизонталь).</summary>
    public double Width { get; }

    /// <summary>Высота внутреннего проёма (вертикаль).</summary>
    public double Height { get; }

    /// <summary>Угол BottomLeft в 3D (для вычисления точек вставки).</summary>
    public Point3D BottomLeft3D { get; }

    /// <summary>Угол BottomRight в 3D.</summary>
    public Point3D BottomRight3D { get; }

    /// <summary>Угол TopLeft в 3D.</summary>
    public Point3D TopLeft3D { get; }

    /// <summary>Угол TopRight в 3D.</summary>
    public Point3D TopRight3D { get; }

    /// <summary>true — размеры взяты из EdgeLoops (точные), false — из параметров семейства (приблизительные).</summary>
    public bool IsExact { get; }

    public OpeningDimensions(
        double width, double height,
        Point3D bottomLeft, Point3D bottomRight,
        Point3D topLeft, Point3D topRight,
        bool isExact)
    {
        Width = width;
        Height = height;
        BottomLeft3D = bottomLeft;
        BottomRight3D = bottomRight;
        TopLeft3D = topLeft;
        TopRight3D = topRight;
        IsExact = isExact;
    }
}
