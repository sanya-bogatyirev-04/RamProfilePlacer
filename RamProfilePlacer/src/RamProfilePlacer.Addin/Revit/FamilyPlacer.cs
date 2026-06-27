using Autodesk.Revit.DB;
using RamProfilePlacer.Addin.Infrastructure;
using RamProfilePlacer.Core.Model;

namespace RamProfilePlacer.Addin.Revit;

/// <summary>
/// Размещает экземпляры семейства профиля на внутренней грани стены
/// через doc.Create.NewFamilyInstance и устанавливает параметры.
/// </summary>
internal static class FamilyPlacer
{
    private const double DegToRadFactor = Math.PI / 180.0;
    private const double MiterAngleDeg = 45.0;

    /// <summary>
    /// Размещает один профиль по данным ProfilePlacement.
    /// </summary>
    /// <returns>Созданный FamilyInstance или null при ошибке.</returns>
    public static FamilyInstance? PlaceProfile(
        Document doc,
        FamilySymbol profileSymbol,
        Reference faceRef,
        ProfilePlacement placement)
    {
        // Переводим Core.Point3D → Revit XYZ
        var location = new XYZ(
            placement.InsertionPoint.X,
            placement.InsertionPoint.Y,
            placement.InsertionPoint.Z);

        var refDir = new XYZ(
            placement.ReferenceDirection.X,
            placement.ReferenceDirection.Y,
            placement.ReferenceDirection.Z);

        FamilyInstance fi = doc.Create.NewFamilyInstance(
            faceRef,
            location,
            refDir,
            profileSymbol);

        if (fi == null)
        {
            FileLogger.Instance.Warn($"NewFamilyInstance вернул null для стороны {placement.Side}.");
            return null;
        }

        SetParameter(fi, "ADSK_Размер_Длина", placement.Length);
        SetParameter(fi, "Врезка в начале", MiterAngleDeg * DegToRadFactor);
        SetParameter(fi, "Врезка в конце", MiterAngleDeg * DegToRadFactor);

        // Логируем фактические значения параметров после записи
        var lenParam = fi.LookupParameter("ADSK_Размер_Длина");
        var miter1 = fi.LookupParameter("Врезка в начале");
        var miter2 = fi.LookupParameter("Врезка в конце");
        FileLogger.Instance.Info(
            $"    [PARAMS] ADSK_Размер_Длина={lenParam?.AsDouble():F6} фт " +
            $"({(lenParam?.AsDouble() ?? 0) * 304.8:F1} мм), " +
            $"Врезка начало={miter1?.AsDouble():F4} рад, конец={miter2?.AsDouble():F4} рад");

        return fi;
    }

    private static void SetParameter(FamilyInstance fi, string paramName, double value)
    {
        var param = fi.LookupParameter(paramName);
        if (param == null)
        {
            FileLogger.Instance.Warn(
                $"[PARAM MISS] '{paramName}' не найден в экземпляре '{fi.Symbol.Family.Name}'. " +
                $"Проверяю параметр типа...");
            param = fi.Symbol.LookupParameter(paramName);
            if (param == null)
            {
                FileLogger.Instance.Warn(
                    $"[PARAM MISS] '{paramName}' не найден и в типе '{fi.Symbol.Name}'.");
                return;
            }
            FileLogger.Instance.Info(
                $"[PARAM] '{paramName}' найден как параметр ТИПА (IsReadOnly={param.IsReadOnly}).");
        }
        else
        {
            FileLogger.Instance.Info(
                $"[PARAM] '{paramName}' найден как параметр ЭКЗЕМПЛЯРА " +
                $"(IsReadOnly={param.IsReadOnly}, StorageType={param.StorageType}, " +
                $"текущее значение={param.AsDouble():F6} фт). Записываем {value:F6} фт.");
        }

        if (param.IsReadOnly)
        {
            FileLogger.Instance.Warn(
                $"[PARAM RO] '{paramName}' только для чтения в '{fi.Symbol.Family.Name}'.");
            return;
        }

        bool ok = param.Set(value);
        FileLogger.Instance.Info(
            $"[PARAM SET] '{paramName}' Set({value:F6}) → result={ok}, " +
            $"после Set значение={param.AsDouble():F6} фт ({param.AsDouble() * 304.8:F1} мм)");
    }
}
