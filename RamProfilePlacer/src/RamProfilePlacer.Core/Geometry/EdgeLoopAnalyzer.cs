namespace RamProfilePlacer.Core.Geometry;

/// <summary>
/// Анализирует набор контуров EdgeLoops грани и находит прямоугольник нужного проёма.
/// Работает полностью без Revit API — принимает точки в пространстве грани (U/V).
/// </summary>
public static class EdgeLoopAnalyzer
{
    /// <summary>
    /// Представление одного контура как набора 2D-точек в системе координат грани.
    /// </summary>
    public sealed class FaceLoop
    {
        public IReadOnlyList<Point2D> Points { get; }

        public FaceLoop(IEnumerable<Point2D> points)
        {
            var list = points.ToList();
            if (list.Count < 3)
                throw new ArgumentException("A loop must have at least 3 points.", nameof(points));
            Points = list;
        }

        public BoundingBox2D GetBoundingBox() => BoundingBox2D.FromPoints(Points);
    }

    /// <summary>
    /// Из набора контуров выбирает внешний (с наибольшей площадью BB) и возвращает
    /// внутренний контур, чей BB содержит проекцию точки вставки проёма.
    /// </summary>
    /// <param name="loops">Все контуры грани (внешний + внутренние/отверстия).</param>
    /// <param name="openingProjection">Проекция точки вставки окна/двери на плоскость грани.</param>
    /// <returns>OpeningRectangle для найденного внутреннего контура.</returns>
    /// <exception cref="InvalidOperationException">Если не удалось найти подходящий контур.</exception>
    public static OpeningRectangle FindOpeningRectangle(
        IReadOnlyList<FaceLoop> loops,
        Point2D openingProjection)
    {
        if (loops == null || loops.Count == 0)
            throw new InvalidOperationException("No loops provided to analyze.");

        if (loops.Count == 1)
            throw new InvalidOperationException(
                "Only one loop found — cannot distinguish inner from outer.");

        // Определяем внешний контур по наибольшей площади BB
        var loopsWithBBox = loops
            .Select(l => (Loop: l, BBox: l.GetBoundingBox()))
            .OrderByDescending(x => x.BBox.Area)
            .ToList();

        var outerBBox = loopsWithBBox[0].BBox;
        var innerLoops = loopsWithBBox.Skip(1).ToList();

        // Ищем внутренний контур, чей BB содержит проекцию точки вставки
        foreach (var (loop, bbox) in innerLoops)
        {
            if (bbox.Contains(openingProjection))
                return new OpeningRectangle(bbox);
        }

        // Если точная проверка не сработала — берём ближайший по центру BB
        var closest = innerLoops
            .OrderBy(x => x.BBox.Center.DistanceTo(openingProjection))
            .First();

        return new OpeningRectangle(closest.BBox);
    }

    /// <summary>
    /// Находит внешний контур (наибольшая площадь BB) из списка контуров.
    /// </summary>
    public static FaceLoop FindOuterLoop(IReadOnlyList<FaceLoop> loops)
    {
        if (loops == null || loops.Count == 0)
            throw new ArgumentException("Loops list is empty.", nameof(loops));

        return loops
            .OrderByDescending(l => l.GetBoundingBox().Area)
            .First();
    }

    /// <summary>
    /// Возвращает все внутренние контуры (исключая внешний с максимальной площадью BB).
    /// </summary>
    public static IReadOnlyList<FaceLoop> FindInnerLoops(IReadOnlyList<FaceLoop> loops)
    {
        if (loops == null || loops.Count == 0) return [];

        var outer = FindOuterLoop(loops);
        return loops.Where(l => l != outer).ToList();
    }
}
