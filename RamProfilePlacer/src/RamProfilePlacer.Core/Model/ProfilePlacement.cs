using RamProfilePlacer.Core.Geometry;

namespace RamProfilePlacer.Core.Model;

/// <summary>
/// Описывает данные для размещения одного экземпляра профиля.
/// Все координаты в системе Core (Point3D), длина в тех же единицах, что и входные данные
/// (для Revit Addin — футы).
/// </summary>
public sealed class ProfilePlacement
{
    /// <summary>Сторона проёма, для которой предназначен профиль.</summary>
    public OpeningSide Side { get; }

    /// <summary>Точка вставки (начало профиля).</summary>
    public Point3D InsertionPoint { get; }

    /// <summary>Направляющий вектор — длина профиля растёт в этом направлении.</summary>
    public Point3D Direction { get; }

    /// <summary>Длина профиля (в тех же единицах, что InsertionPoint).</summary>
    public double Length { get; }

    public ProfilePlacement(OpeningSide side, Point3D insertionPoint, Point3D direction, double length)
    {
        if (length <= 0) throw new ArgumentException("Length must be positive.", nameof(length));

        Side = side;
        InsertionPoint = insertionPoint;
        Direction = direction;
        Length = length;
    }

    public override string ToString() =>
        $"ProfilePlacement[{Side}] at {InsertionPoint}, dir={Direction}, len={Length:F4}";
}
