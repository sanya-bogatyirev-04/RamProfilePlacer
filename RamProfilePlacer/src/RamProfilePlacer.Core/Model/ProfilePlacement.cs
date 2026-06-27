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

    /// <summary>Точка вставки (центр профиля).</summary>
    public Point3D InsertionPoint { get; }

    /// <summary>Направление длины профиля (вдоль стороны проёма).</summary>
    public Point3D Direction { get; }

    /// <summary>
    /// Вектор, передаваемый в NewFamilyInstance как referenceDirection.
    /// Перпендикулярен Direction в плоскости грани.
    /// </summary>
    public Point3D ReferenceDirection { get; }

    /// <summary>Длина профиля (в тех же единицах, что InsertionPoint).</summary>
    public double Length { get; }

    public ProfilePlacement(OpeningSide side, Point3D insertionPoint, Point3D direction,
        Point3D referenceDirection, double length)
    {
        if (length <= 0) throw new ArgumentException("Length must be positive.", nameof(length));

        Side = side;
        InsertionPoint = insertionPoint;
        Direction = direction;
        ReferenceDirection = referenceDirection;
        Length = length;
    }

    public override string ToString() =>
        $"ProfilePlacement[{Side}] at {InsertionPoint}, dir={Direction}, len={Length:F4}";
}
