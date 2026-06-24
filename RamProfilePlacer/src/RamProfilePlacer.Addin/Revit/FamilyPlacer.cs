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
        // Активируем символ, если не активирован
        if (!profileSymbol.IsActive)
            profileSymbol.Activate();

        // Переводим Core.Point3D → Revit XYZ
        var location = new XYZ(
            placement.InsertionPoint.X,
            placement.InsertionPoint.Y,
            placement.InsertionPoint.Z);

        var refDir = new XYZ(
            placement.Direction.X,
            placement.Direction.Y,
            placement.Direction.Z);

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

        // Устанавливаем параметры
        SetParameter(fi, "ADSK_Размер_Длина", placement.Length);
        SetParameter(fi, "Врезка в начале", MiterAngleDeg * DegToRadFactor);
        SetParameter(fi, "Врезка в конце", MiterAngleDeg * DegToRadFactor);

        return fi;
    }

    private static void SetParameter(FamilyInstance fi, string paramName, double value)
    {
        var param = fi.LookupParameter(paramName);
        if (param == null)
        {
            FileLogger.Instance.Warn(
                $"Параметр '{paramName}' не найден в семействе '{fi.Symbol.Family.Name}'. " +
                $"Значение не установлено.");
            return;
        }

        if (param.IsReadOnly)
        {
            FileLogger.Instance.Warn(
                $"Параметр '{paramName}' доступен только для чтения в семействе '{fi.Symbol.Family.Name}'.");
            return;
        }

        param.Set(value);
    }
}
