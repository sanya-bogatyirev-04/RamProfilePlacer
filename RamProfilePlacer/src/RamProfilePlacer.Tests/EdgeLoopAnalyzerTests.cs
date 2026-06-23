using RamProfilePlacer.Core.Geometry;
using Xunit;
using static RamProfilePlacer.Core.Geometry.EdgeLoopAnalyzer;

namespace RamProfilePlacer.Tests;

public sealed class EdgeLoopAnalyzerTests
{
    // -------------------------------------------------------------------------
    // Вспомогательный метод: прямоугольный контур
    // -------------------------------------------------------------------------

    private static FaceLoop MakeRect(double minU, double maxU, double minV, double maxV)
    {
        return new FaceLoop(new[]
        {
            new Point2D(minU, minV),
            new Point2D(maxU, minV),
            new Point2D(maxU, maxV),
            new Point2D(minU, maxV)
        });
    }

    // -------------------------------------------------------------------------
    // FindOuterLoop: внешний контур — наибольшая площадь
    // -------------------------------------------------------------------------

    [Fact]
    public void FindOuterLoop_ReturnsLoopWithLargestBBoxArea()
    {
        var outer = MakeRect(0, 10, 0, 8);   // площадь 80
        var inner = MakeRect(2, 5, 1, 4);    // площадь 9
        var loops = new List<FaceLoop> { inner, outer };

        var result = FindOuterLoop(loops);

        Assert.Same(outer, result);
    }

    [Fact]
    public void FindInnerLoops_ExcludesOuterLoop()
    {
        var outer = MakeRect(0, 10, 0, 8);
        var inner1 = MakeRect(1, 3, 1, 3);
        var inner2 = MakeRect(6, 8, 1, 3);
        var loops = new List<FaceLoop> { outer, inner1, inner2 };

        var inner = FindInnerLoops(loops);

        Assert.Equal(2, inner.Count);
        Assert.DoesNotContain(outer, inner);
    }

    // -------------------------------------------------------------------------
    // FindOpeningRectangle: выбор нужного контура по точке вставки
    // -------------------------------------------------------------------------

    [Fact]
    public void FindOpeningRectangle_SingleInner_ReturnsItRegardlessOfProjection()
    {
        var outer = MakeRect(0, 10, 0, 8);
        var inner = MakeRect(3, 7, 2, 6);
        var loops = new List<FaceLoop> { outer, inner };

        // Проекция внутри внутреннего контура
        var rect = FindOpeningRectangle(loops, new Point2D(5, 4));

        Assert.Equal(4.0, rect.Width, precision: 9);
        Assert.Equal(4.0, rect.Height, precision: 9);
    }

    [Fact]
    public void FindOpeningRectangle_TwoInner_SelectsByProjection()
    {
        var outer = MakeRect(0, 20, 0, 8);
        var left = MakeRect(1, 5, 1, 5);     // ширина 4
        var right = MakeRect(12, 18, 1, 5);  // ширина 6

        var loops = new List<FaceLoop> { outer, left, right };

        // Точка вставки — в правом проёме
        var rect = FindOpeningRectangle(loops, new Point2D(15, 3));

        Assert.Equal(6.0, rect.Width, precision: 9);
        Assert.Equal(4.0, rect.Height, precision: 9);
    }

    [Fact]
    public void FindOpeningRectangle_ProjectionInLeftWindow_ReturnsLeftRect()
    {
        var outer = MakeRect(0, 20, 0, 8);
        var left = MakeRect(1, 4, 1, 5);
        var right = MakeRect(12, 18, 1, 5);
        var loops = new List<FaceLoop> { outer, left, right };

        var rect = FindOpeningRectangle(loops, new Point2D(2.5, 3));

        Assert.Equal(3.0, rect.Width, precision: 9);
    }

    // -------------------------------------------------------------------------
    // Ширина/высота: нижний/верхний ↔ левый/правый
    // -------------------------------------------------------------------------

    [Fact]
    public void OpeningRectangle_Width_MapsToHorizontal_Height_ToVertical()
    {
        var outer = MakeRect(0, 20, 0, 10);
        var inner = MakeRect(2, 8, 1, 6);  // ширина=6, высота=5
        var loops = new List<FaceLoop> { outer, inner };

        var rect = FindOpeningRectangle(loops, new Point2D(5, 3));

        // Ширина — нижний/верхний профиль
        Assert.Equal(6.0, rect.Width, precision: 9);
        // Высота — левый/правый профиль
        Assert.Equal(5.0, rect.Height, precision: 9);
    }

    // -------------------------------------------------------------------------
    // Вырожденные данные
    // -------------------------------------------------------------------------

    [Fact]
    public void FindOpeningRectangle_EmptyLoops_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() =>
            FindOpeningRectangle([], new Point2D(5, 5)));
    }

    [Fact]
    public void FindOpeningRectangle_OneLoop_ThrowsInvalidOperationException()
    {
        var loop = MakeRect(0, 10, 0, 5);
        Assert.Throws<InvalidOperationException>(() =>
            FindOpeningRectangle([loop], new Point2D(5, 2.5)));
    }

    [Fact]
    public void FaceLoop_LessThanThreePoints_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            new FaceLoop([new Point2D(0, 0), new Point2D(1, 1)]));
    }
}
