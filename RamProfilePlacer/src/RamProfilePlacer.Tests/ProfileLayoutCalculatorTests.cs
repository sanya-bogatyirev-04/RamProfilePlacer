using RamProfilePlacer.Core;
using RamProfilePlacer.Core.Geometry;
using RamProfilePlacer.Core.Model;
using Xunit;

namespace RamProfilePlacer.Tests;

public sealed class ProfileLayoutCalculatorTests
{
    // -------------------------------------------------------------------------
    // Вспомогательный проём: 3 x 2 (ширина x высота), начало в (0,0,0)
    // -------------------------------------------------------------------------

    private static OpeningDimensions MakeDims(
        double width = 3.0, double height = 2.0,
        double originX = 0, double originY = 0, double originZ = 0)
    {
        var bl = new Point3D(originX, originY, originZ);
        var br = new Point3D(originX + width, originY, originZ);
        var tl = new Point3D(originX, originY, originZ + height);
        var tr = new Point3D(originX + width, originY, originZ + height);

        return new OpeningDimensions(width, height, bl, br, tl, tr, isExact: true);
    }

    // -------------------------------------------------------------------------
    // Количество профилей
    // -------------------------------------------------------------------------

    [Fact]
    public void Calculate_Window_Returns4Placements()
    {
        var dims = MakeDims();
        var placements = ProfileLayoutCalculator.Calculate(dims, isDoor: false);
        Assert.Equal(4, placements.Count);
    }

    [Fact]
    public void Calculate_Door_Returns3Placements_NoBottom()
    {
        var dims = MakeDims();
        var placements = ProfileLayoutCalculator.Calculate(dims, isDoor: true);
        Assert.Equal(3, placements.Count);
        Assert.DoesNotContain(placements, p => p.Side == OpeningSide.Bottom);
    }

    // -------------------------------------------------------------------------
    // Стороны присутствуют / отсутствуют
    // -------------------------------------------------------------------------

    [Fact]
    public void Calculate_Window_HasAllFourSides()
    {
        var dims = MakeDims();
        var placements = ProfileLayoutCalculator.Calculate(dims, isDoor: false);
        var sides = placements.Select(p => p.Side).ToHashSet();

        Assert.Contains(OpeningSide.Bottom, sides);
        Assert.Contains(OpeningSide.Top, sides);
        Assert.Contains(OpeningSide.Left, sides);
        Assert.Contains(OpeningSide.Right, sides);
    }

    [Fact]
    public void Calculate_Door_HasTopLeftRight()
    {
        var dims = MakeDims();
        var placements = ProfileLayoutCalculator.Calculate(dims, isDoor: true);
        var sides = placements.Select(p => p.Side).ToHashSet();

        Assert.Contains(OpeningSide.Top, sides);
        Assert.Contains(OpeningSide.Left, sides);
        Assert.Contains(OpeningSide.Right, sides);
    }

    // -------------------------------------------------------------------------
    // Длины профилей
    // -------------------------------------------------------------------------

    [Fact]
    public void Calculate_BottomTop_LengthEqualsWidth()
    {
        var dims = MakeDims(width: 3.0, height: 2.0);
        var placements = ProfileLayoutCalculator.Calculate(dims, isDoor: false);

        var bottom = placements.Single(p => p.Side == OpeningSide.Bottom);
        var top = placements.Single(p => p.Side == OpeningSide.Top);

        Assert.Equal(3.0, bottom.Length, precision: 9);
        Assert.Equal(3.0, top.Length, precision: 9);
    }

    [Fact]
    public void Calculate_LeftRight_LengthEqualsHeight()
    {
        var dims = MakeDims(width: 3.0, height: 2.0);
        var placements = ProfileLayoutCalculator.Calculate(dims, isDoor: false);

        var left = placements.Single(p => p.Side == OpeningSide.Left);
        var right = placements.Single(p => p.Side == OpeningSide.Right);

        Assert.Equal(2.0, left.Length, precision: 9);
        Assert.Equal(2.0, right.Length, precision: 9);
    }

    // -------------------------------------------------------------------------
    // Точки вставки
    // -------------------------------------------------------------------------

    [Fact]
    public void Calculate_Bottom_InsertionPointIsBottomLeft()
    {
        var dims = MakeDims();
        var placements = ProfileLayoutCalculator.Calculate(dims, isDoor: false);
        var bottom = placements.Single(p => p.Side == OpeningSide.Bottom);

        Assert.Equal(dims.BottomLeft3D, bottom.InsertionPoint);
    }

    [Fact]
    public void Calculate_Top_InsertionPointIsTopLeft()
    {
        var dims = MakeDims();
        var placements = ProfileLayoutCalculator.Calculate(dims, isDoor: false);
        var top = placements.Single(p => p.Side == OpeningSide.Top);

        Assert.Equal(dims.TopLeft3D, top.InsertionPoint);
    }

    [Fact]
    public void Calculate_Left_InsertionPointIsBottomLeft()
    {
        var dims = MakeDims();
        var placements = ProfileLayoutCalculator.Calculate(dims, isDoor: false);
        var left = placements.Single(p => p.Side == OpeningSide.Left);

        Assert.Equal(dims.BottomLeft3D, left.InsertionPoint);
    }

    [Fact]
    public void Calculate_Right_InsertionPointIsBottomRight()
    {
        var dims = MakeDims();
        var placements = ProfileLayoutCalculator.Calculate(dims, isDoor: false);
        var right = placements.Single(p => p.Side == OpeningSide.Right);

        Assert.Equal(dims.BottomRight3D, right.InsertionPoint);
    }

    // -------------------------------------------------------------------------
    // Направляющие векторы (нормализованы, единичная длина)
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Calculate_AllDirections_AreUnitVectors(bool isDoor)
    {
        var dims = MakeDims();
        var placements = ProfileLayoutCalculator.Calculate(dims, isDoor);

        foreach (var p in placements)
        {
            double len = Math.Sqrt(
                p.Direction.X * p.Direction.X +
                p.Direction.Y * p.Direction.Y +
                p.Direction.Z * p.Direction.Z);
            Assert.Equal(1.0, len, precision: 9);
        }
    }

    [Fact]
    public void Calculate_Bottom_DirectionPointsRightAlongX()
    {
        // BottomLeft → BottomRight: направление +X
        var dims = MakeDims();
        var placements = ProfileLayoutCalculator.Calculate(dims, isDoor: false);
        var bottom = placements.Single(p => p.Side == OpeningSide.Bottom);

        Assert.Equal(1.0, bottom.Direction.X, precision: 9);
        Assert.Equal(0.0, bottom.Direction.Y, precision: 9);
        Assert.Equal(0.0, bottom.Direction.Z, precision: 9);
    }

    [Fact]
    public void Calculate_Left_DirectionPointsUpAlongZ()
    {
        // BottomLeft → TopLeft: направление +Z (в нашем тесте вертикаль = Z)
        var dims = MakeDims();
        var placements = ProfileLayoutCalculator.Calculate(dims, isDoor: false);
        var left = placements.Single(p => p.Side == OpeningSide.Left);

        Assert.Equal(0.0, left.Direction.X, precision: 9);
        Assert.Equal(0.0, left.Direction.Y, precision: 9);
        Assert.Equal(1.0, left.Direction.Z, precision: 9);
    }

    // -------------------------------------------------------------------------
    // Смещённый проём (проверяем что origin учитывается)
    // -------------------------------------------------------------------------

    [Fact]
    public void Calculate_OffsetOpening_InsertionPointsCorrect()
    {
        var dims = MakeDims(width: 2.0, height: 3.0, originX: 10, originY: 5, originZ: 1);
        var placements = ProfileLayoutCalculator.Calculate(dims, isDoor: false);

        var bottom = placements.Single(p => p.Side == OpeningSide.Bottom);
        Assert.Equal(10.0, bottom.InsertionPoint.X, precision: 9);
        Assert.Equal(5.0, bottom.InsertionPoint.Y, precision: 9);
        Assert.Equal(1.0, bottom.InsertionPoint.Z, precision: 9);
    }

    // -------------------------------------------------------------------------
    // Отрицательная длина — ProfilePlacement должен кинуть ArgumentException
    // -------------------------------------------------------------------------

    [Fact]
    public void ProfilePlacement_NegativeLength_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            new ProfilePlacement(
                OpeningSide.Bottom,
                new Point3D(0, 0, 0),
                new Point3D(1, 0, 0),
                length: -1.0));
    }

    // -------------------------------------------------------------------------
    // Point3D.ProjectOnPlane — явная проекция
    // -------------------------------------------------------------------------

    [Fact]
    public void Point3D_ProjectOnPlane_PointAbovePlane_ProjectsCorrectly()
    {
        // Плоскость: Z=0 (normal=(0,0,1), origin=(0,0,0))
        var point = new Point3D(3.0, 4.0, 5.0);
        var normal = new Point3D(0, 0, 1);
        var origin = new Point3D(0, 0, 0);

        var projected = point.ProjectOnPlane(origin, normal);

        Assert.Equal(3.0, projected.X, precision: 9);
        Assert.Equal(4.0, projected.Y, precision: 9);
        Assert.Equal(0.0, projected.Z, precision: 9);
    }

    [Fact]
    public void Point3D_ProjectOnPlane_PointAlreadyOnPlane_Unchanged()
    {
        var point = new Point3D(3.0, 4.0, 0.0);
        var normal = new Point3D(0, 0, 1);
        var origin = new Point3D(0, 0, 0);

        var projected = point.ProjectOnPlane(origin, normal);

        Assert.Equal(3.0, projected.X, precision: 9);
        Assert.Equal(4.0, projected.Y, precision: 9);
        Assert.Equal(0.0, projected.Z, precision: 9);
    }

    [Fact]
    public void Point3D_ProjectOnPlane_TiltedPlane_WorksCorrectly()
    {
        // Наклонная плоскость: нормаль (1,0,0), проходит через (2,0,0)
        // Проекция точки (5,3,4) → (2,3,4)
        var point = new Point3D(5, 3, 4);
        var normal = new Point3D(1, 0, 0);
        var origin = new Point3D(2, 0, 0);

        var projected = point.ProjectOnPlane(origin, normal);

        Assert.Equal(2.0, projected.X, precision: 9);
        Assert.Equal(3.0, projected.Y, precision: 9);
        Assert.Equal(4.0, projected.Z, precision: 9);
    }
}
