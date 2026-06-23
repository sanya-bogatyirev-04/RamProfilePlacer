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
    public static (Face face, Reference faceRef) GetInteriorFace(Wall wall)
    {
        IList<Reference> refs = HostObjectUtils.GetSideFaces(wall, ShellLayerType.Interior);
        if (refs == null || refs.Count == 0)
            throw new InvalidOperationException($"Wall {wall.Id} has no interior side faces.");

        Reference faceRef = refs[0]; // сохраняем исходную ссылку
        Face face = wall.GetGeometryObjectFromReference(faceRef) as Face
            ?? throw new InvalidOperationException($"Cannot get Face from reference for wall {wall.Id}.");

        // ВНИМАНИЕ: face.Reference == null — для NewFamilyInstance нужен именно faceRef
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
        Face face,
        XYZ openingLocation,
        FamilyInstance opening,
        out bool usedFallback)
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
                // Переход на запасной путь
                usedFallback = true;
                _ = ex; // подавляем предупреждение
            }
        }
        else
        {
            usedFallback = true;
        }

        // Запасной путь — параметры семейства
        return GetDimensionsFromFamilyParameters(opening);
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

        var bl = ToWorld(rect.BottomLeft);
        var br = ToWorld(rect.BottomRight);
        var tl = ToWorld(rect.TopLeft);
        var tr = ToWorld(rect.TopRight);

        return new OpeningDimensions(
            rect.Width, rect.Height,
            bl, br, tl, tr,
            isExact: true);
    }

    private static OpeningDimensions GetDimensionsFromFamilyParameters(FamilyInstance fi)
    {
        // Параметры описывают наружный проём (меньше внутреннего из-за четвертей) — запасной путь
        double width = 0, height = 0;

        var wp = fi.LookupParameter("WINDOW_WIDTH") ?? fi.get_Parameter(BuiltInParameter.WINDOW_WIDTH);
        var hp = fi.LookupParameter("WINDOW_HEIGHT") ?? fi.get_Parameter(BuiltInParameter.WINDOW_HEIGHT);

        if (wp == null || hp == null)
        {
            // Двери
            wp = fi.LookupParameter("DOOR_WIDTH") ?? fi.get_Parameter(BuiltInParameter.DOOR_WIDTH);
            hp = fi.LookupParameter("DOOR_HEIGHT") ?? fi.get_Parameter(BuiltInParameter.DOOR_HEIGHT);
        }

        if (wp != null) width = wp.AsDouble();
        if (hp != null) height = hp.AsDouble();

        if (width < 1e-9 || height < 1e-9)
            throw new InvalidOperationException("Cannot determine opening dimensions from family parameters.");

        // Вычисляем угловые точки из точки вставки и трансформации
        var transform = fi.GetTransform();
        XYZ loc = (fi.Location as LocationPoint)?.Point ?? XYZ.Zero;

        // Строим приближённые углы от центра проёма
        XYZ right = transform.BasisX;
        XYZ up = XYZ.BasisZ;

        XYZ center = loc;
        XYZ halfW = right * (width / 2.0);

        Point3D ToCore(XYZ p) => new(p.X, p.Y, p.Z);

        return new OpeningDimensions(
            width, height,
            ToCore(center - halfW),
            ToCore(center + halfW),
            ToCore(center - halfW + up * height),
            ToCore(center + halfW + up * height),
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
}
