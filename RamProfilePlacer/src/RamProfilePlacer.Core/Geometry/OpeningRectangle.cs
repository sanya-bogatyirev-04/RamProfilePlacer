namespace RamProfilePlacer.Core.Geometry;

/// <summary>
/// Описывает внутренний прямоугольник проёма в координатах плоскости грани (U/V).
/// Width — горизонтальный размер (нижний/верхний профиль).
/// Height — вертикальный размер (левый/правый профиль).
/// </summary>
public sealed class OpeningRectangle
{
    /// <summary>Ширина проёма (горизонталь, направление U).</summary>
    public double Width { get; }

    /// <summary>Высота проёма (вертикаль, направление V).</summary>
    public double Height { get; }

    /// <summary>Угол Bottom-Left (минимальный U, минимальный V).</summary>
    public Point2D BottomLeft { get; }

    /// <summary>Угол Bottom-Right.</summary>
    public Point2D BottomRight { get; }

    /// <summary>Угол Top-Left.</summary>
    public Point2D TopLeft { get; }

    /// <summary>Угол Top-Right.</summary>
    public Point2D TopRight { get; }

    /// <summary>Исходный BoundingBox.</summary>
    public BoundingBox2D BoundingBox { get; }

    public OpeningRectangle(BoundingBox2D bbox)
    {
        BoundingBox = bbox ?? throw new ArgumentNullException(nameof(bbox));
        Width = bbox.Width;
        Height = bbox.Height;

        var (bl, br, tr, tl) = bbox.GetCorners();
        BottomLeft = bl;
        BottomRight = br;
        TopRight = tr;
        TopLeft = tl;
    }

    public override string ToString() =>
        $"OpeningRectangle W={Width:F4} H={Height:F4}";
}
