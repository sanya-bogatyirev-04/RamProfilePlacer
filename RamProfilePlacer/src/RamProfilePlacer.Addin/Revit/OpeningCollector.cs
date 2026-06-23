using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace RamProfilePlacer.Addin.Revit;

/// <summary>
/// Фильтр выбора: разрешает выбирать только окна и двери.
/// </summary>
internal sealed class OpeningSelectionFilter : ISelectionFilter
{
    public bool AllowElement(Element elem)
    {
        return elem.Category?.Id.Value == (long)BuiltInCategory.OST_Windows ||
               elem.Category?.Id.Value == (long)BuiltInCategory.OST_Doors;
    }

    public bool AllowReference(Reference reference, XYZ position) => false;
}
