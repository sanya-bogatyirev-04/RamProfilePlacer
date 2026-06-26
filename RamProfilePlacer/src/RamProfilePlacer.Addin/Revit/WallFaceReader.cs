using Autodesk.Revit.DB;
using RamProfilePlacer.Core.Geometry;
using RamProfilePlacer.Core.Model;

namespace RamProfilePlacer.Addin.Revit;

/// <summary>
/// Читает внутреннюю грань стены, EdgeLoops и определяет размеры проёма.
/// Переводит данные Revit API в типы Core.
/// </summary>
internal static class WallFaceReader
{
    /// <summary>
    /// Возвращает внутреннюю грань стены и её исходную Reference.
    /// ВАЖНО: faceRef — исходная ссылка из GetSideFaces, нужна для NewFamilyInstance.
    /// </summary>
    public static (Face? face, Reference faceRef) GetInteriorFace(Wall wall)
    {
        IList<Reference> refs = HostObjectUtils.GetSideFaces(wall, ShellLayerType.Interior);
        if (refs == null || refs.Count == 0)
            throw new InvalidOperationException($"Wall {wall.Id} has no interior side faces.");

        Reference faceRef = refs[0];
        Face? face = wall.GetGeometryObjectFromReference(faceRef) as Face;

        return (face, faceRef);
    }

    /// <summary>
    /// Определяет размеры внутреннего проёма из EdgeLoops грани.
    /// Основной путь: EdgeLoops → BoundingBox в плоскости грани.
    /// </summary>
    /// <param name="face">Внутренняя грань стены (PlanarFace).</param>
    /// <param name="openingLocation">Точка вставки окна/двери (в координатах Revit, футы).</param>
    /// <param name="usedFallback">true если использован запасной путь.</param>
    /// <returns>OpeningDimensions с 3D-углами в футах.</returns>
    public static OpeningDimensions GetOpeningDimensions(
        Face? face,
        XYZ openingLocation,
        FamilyInstance opening,
        out bool usedFallback,
        Infrastructure.FileLogger? logger = null)
    {
        usedFallback = false;

        if (face is PlanarFace planarFace)
        {
            try
            {
                return GetDimensionsFromEdgeLoops(planarFace, openingLocation);
            }
            catch (Exception ex)
            {
                usedFallback = true;
                logger?.Warn(
                    $"  Анализ EdgeLoops не удался для ElementId={opening.Id}, переход на запасной путь. " +
                    $"Причина: {ex.GetType().Name}: {ex.Message}");
            }
        }
        else
        {
            usedFallback = true;
            if (face == null)
                logger?.Warn($"  Грань равна null для ElementId={opening.Id}, используется запасной путь.");
            else
                logger?.Warn($"  Грань не является PlanarFace (тип={face.GetType().Name}) для ElementId={opening.Id}, используется запасной путь.");
        }

        // Запасной путь — параметры семейства; если есть PlanarFace, проецируем на грань
        return GetDimensionsFromFamilyParameters(opening, face as PlanarFace);
    }

    private static OpeningDimensions GetDimensionsFromEdgeLoops(PlanarFace pf, XYZ openingLocation)
    {
        // Оси грани
        XYZ faceOrigin = pf.Origin;
        XYZ faceNormal = pf.FaceNormal;
        XYZ faceU = pf.XVector;  // горизонталь
        XYZ faceV = pf.YVector;  // вертикаль

        // Собираем все контуры как FaceLoop (в 2D-координатах грани U/V)
        var loops = new List<Core.Geometry.EdgeLoopAnalyzer.FaceLoop>();
        foreach (EdgeArray edgeArray in pf.EdgeLoops)
        {
            var pts2D = new List<Point2D>();
            foreach (Edge edge in edgeArray)
            {
                foreach (XYZ pt in edge.Tessellate())
                {
                    var p2 = ProjectToFacePlane2D(pt, faceOrigin, faceNormal, faceU, faceV);
                    pts2D.Add(p2);
                }
            }
            if (pts2D.Count >= 3)
                loops.Add(new Core.Geometry.EdgeLoopAnalyzer.FaceLoop(pts2D));
        }

        if (loops.Count < 2)
            throw new InvalidOperationException("Not enough loops to identify opening (need at least 2).");

        // Проецируем точку вставки проёма на плоскость грани (2D)
        var projected3D = ProjectOnPlane(openingLocation, faceOrigin, faceNormal);
        var projectedUV = ProjectToFacePlane2D(projected3D, faceOrigin, faceNormal, faceU, faceV);

        // Находим прямоугольник нужного проёма
        var rect = Core.Geometry.EdgeLoopAnalyzer.FindOpeningRectangle(loops, projectedUV);

        // Переводим углы 2D → 3D (на плоскости грани)
        Point3D ToWorld(Point2D p) => new(
            faceOrigin.X + faceU.X * p.U + faceV.X * p.V,
            faceOrigin.Y + faceU.Y * p.U + faceV.Y * p.V,
            faceOrigin.Z + faceU.Z * p.U + faceV.Z * p.V);

        // Определяем углы по мировому Z, а не по MinV/MaxV —
        // знак YVector грани зависит от ориентации стены и не гарантирован.
        var corners = new[] { ToWorld(rect.BottomLeft), ToWorld(rect.BottomRight),
                              ToWorld(rect.TopLeft), ToWorld(rect.TopRight) };
        var sorted = corners.OrderBy(c => c.Z).ThenBy(c => c.X).ThenBy(c => c.Y).ToArray();
        var bottomPair = sorted.Take(2).OrderBy(c => DotXY(c, faceU)).ToArray();
        var topPair = sorted.Skip(2).OrderBy(c => DotXY(c, faceU)).ToArray();

        var bl = bottomPair[0];
        var br = bottomPair[1];
        var tl = topPair[0];
        var tr = topPair[1];

        double width = Distance(bl, br);
        double height = Distance(bl, tl);

        return new OpeningDimensions(
            width, height,
            bl, br, tl, tr,
            isExact: true);
    }

    private static OpeningDimensions GetDimensionsFromFamilyParameters(FamilyInstance fi, PlanarFace? pf = null)
    {
        // WINDOW_WIDTH/HEIGHT и DOOR_WIDTH/HEIGHT — параметры типа, читаем из fi.Symbol.
        double width = 0, height = 0;

        var wp = fi.Symbol.get_Parameter(BuiltInParameter.WINDOW_WIDTH)
                 ?? fi.Symbol.LookupParameter("WINDOW_WIDTH");
        var hp = fi.Symbol.get_Parameter(BuiltInParameter.WINDOW_HEIGHT)
                 ?? fi.Symbol.LookupParameter("WINDOW_HEIGHT");

        if (wp == null || hp == null)
        {
            wp = fi.Symbol.get_Parameter(BuiltInParameter.DOOR_WIDTH)
                 ?? fi.Symbol.LookupParameter("DOOR_WIDTH");
            hp = fi.Symbol.get_Parameter(BuiltInParameter.DOOR_HEIGHT)
                 ?? fi.Symbol.LookupParameter("DOOR_HEIGHT");
        }

        if (wp != null) width = wp.AsDouble();
        if (hp != null) height = hp.AsDouble();

        if (width < 1e-9 || height < 1e-9)
            throw new InvalidOperationException("Cannot determine opening dimensions from family parameters.");

        var transform = fi.GetTransform();
        XYZ loc = (fi.Location as LocationPoint)?.Point ?? XYZ.Zero;

        XYZ right = transform.BasisX;
        XYZ up = XYZ.BasisZ;

        XYZ center = loc;
        XYZ halfW = right * (width / 2.0);

        XYZ blXYZ = center - halfW;
        XYZ brXYZ = center + halfW;
        XYZ tlXYZ = center - halfW + up * height;
        XYZ trXYZ = center + halfW + up * height;

        // Проецируем углы на плоскость внутренней грани, если она доступна —
        // NewFamilyInstance(Reference, location, …) ожидает точку на грани.
        if (pf != null)
        {
            XYZ faceOrigin = pf.Origin;
            XYZ faceNormal = pf.FaceNormal;
            blXYZ = ProjectOnPlane(blXYZ, faceOrigin, faceNormal);
            brXYZ = ProjectOnPlane(brXYZ, faceOrigin, faceNormal);
            tlXYZ = ProjectOnPlane(tlXYZ, faceOrigin, faceNormal);
            trXYZ = ProjectOnPlane(trXYZ, faceOrigin, faceNormal);
        }

        Point3D ToCore(XYZ p) => new(p.X, p.Y, p.Z);

        return new OpeningDimensions(
            width, height,
            ToCore(blXYZ), ToCore(brXYZ),
            ToCore(tlXYZ), ToCore(trXYZ),
            isExact: false);
    }

    /// <summary>
    /// Явная проекция точки p на плоскость (faceOrigin, faceNormal).
    /// Используется вместо Face.Project() — тот не работает над отверстиями.
    /// </summary>
    public static XYZ ProjectOnPlane(XYZ p, XYZ faceOrigin, XYZ faceNormal)
    {
        var diff = p - faceOrigin;
        var dist = diff.DotProduct(faceNormal);
        return p - faceNormal.Multiply(dist);
    }

    /// <summary>
    /// Проецирует 3D-точку на плоскость грани и возвращает 2D-координаты (U, V).
    /// </summary>
    private static Point2D ProjectToFacePlane2D(
        XYZ pt, XYZ origin, XYZ normal, XYZ axisU, XYZ axisV)
    {
        var projected = ProjectOnPlane(pt, origin, normal);
        var diff = projected - origin;
        return new Point2D(diff.DotProduct(axisU), diff.DotProduct(axisV));
    }

    private static double DotXY(Point3D p, XYZ axis) =>
        p.X * axis.X + p.Y * axis.Y + p.Z * axis.Z;

    private static double Distance(Point3D a, Point3D b) =>
        Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y) + (a.Z - b.Z) * (a.Z - b.Z));
}
