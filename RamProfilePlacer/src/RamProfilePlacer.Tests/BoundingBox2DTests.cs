using RamProfilePlacer.Core.Geometry;
using Xunit;

namespace RamProfilePlacer.Tests;

public sealed class BoundingBox2DTests
{
    // -------------------------------------------------------------------------
    // Создание из точек
    // -------------------------------------------------------------------------

    [Fact]
    public void FromPoints_BasicRectangle_CorrectBounds()
    {
        var pts = new[]
        {
            new Point2D(1, 2), new Point2D(5, 2),
            new Point2D(5, 8), new Point2D(1, 8)
        };
        var bb = BoundingBox2D.FromPoints(pts);

        Assert.Equal(1, bb.MinU);
        Assert.Equal(5, bb.MaxU);
        Assert.Equal(2, bb.MinV);
        Assert.Equal(8, bb.MaxV);
    }

    [Fact]
    public void FromPoints_Width_Height_Area_AreCorrect()
    {
        var bb = BoundingBox2D.FromPoints([new(0, 0), new(3, 0), new(3, 4), new(0, 4)]);

        Assert.Equal(3.0, bb.Width, precision: 9);
        Assert.Equal(4.0, bb.Height, precision: 9);
        Assert.Equal(12.0, bb.Area, precision: 9);
    }

    [Fact]
    public void FromPoints_SinglePoint_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            BoundingBox2D.FromPoints([new Point2D(1, 1)]));
    }

    [Fact]
    public void FromPoints_NegativeCoordinates_WorksCorrectly()
    {
        var bb = BoundingBox2D.FromPoints([new(-3, -5), new(2, 1)]);

        Assert.Equal(-3, bb.MinU);
        Assert.Equal(2, bb.MaxU);
        Assert.Equal(-5, bb.MinV);
        Assert.Equal(1, bb.MaxV);
    }

    // -------------------------------------------------------------------------
    // Contains
    // -------------------------------------------------------------------------

    [Fact]
    public void Contains_PointInside_ReturnsTrue()
    {
        var bb = new BoundingBox2D(0, 10, 0, 5);
        Assert.True(bb.Contains(new Point2D(5, 2.5)));
    }

    [Fact]
    public void Contains_PointOutside_ReturnsFalse()
    {
        var bb = new BoundingBox2D(0, 10, 0, 5);
        Assert.False(bb.Contains(new Point2D(15, 2.5)));
    }

    [Fact]
    public void Contains_PointOnBoundary_ReturnsTrueWithTolerance()
    {
        var bb = new BoundingBox2D(0, 10, 0, 5);
        Assert.True(bb.Contains(new Point2D(10, 5)));
    }

    [Fact]
    public void Contains_PointJustOutsideTolerance_ReturnsFalse()
    {
        var bb = new BoundingBox2D(0, 10, 0, 5);
        Assert.False(bb.Contains(new Point2D(10.01, 5), tolerance: 1e-6));
    }

    // -------------------------------------------------------------------------
    // GetCorners
    // -------------------------------------------------------------------------

    [Fact]
    public void GetCorners_ReturnsCorrectFourCorners()
    {
        var bb = new BoundingBox2D(1, 4, 2, 6);
        var (bl, br, tr, tl) = bb.GetCorners();

        Assert.Equal(new Point2D(1, 2), bl);
        Assert.Equal(new Point2D(4, 2), br);
        Assert.Equal(new Point2D(4, 6), tr);
        Assert.Equal(new Point2D(1, 6), tl);
    }

    // -------------------------------------------------------------------------
    // Конструктор: некорректные аргументы
    // -------------------------------------------------------------------------

    [Fact]
    public void Constructor_MaxLessThanMin_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new BoundingBox2D(5, 1, 0, 10));
    }

    // -------------------------------------------------------------------------
    // Area: нулевой размер
    // -------------------------------------------------------------------------

    [Fact]
    public void Area_ZeroHeight_ReturnsZero()
    {
        var bb = new BoundingBox2D(0, 5, 3, 3);
        Assert.Equal(0.0, bb.Area);
    }
}
