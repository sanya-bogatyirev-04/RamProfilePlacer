using Autodesk.Revit.DB;

namespace RamProfilePlacer.Addin.Revit;

/// <summary>
/// Подавляет предупреждения Revit уровня Warning во время транзакции,
/// чтобы модальные окна не прерывали пакетное размещение профилей.
/// </summary>
internal sealed class WarningSwallower : IFailuresPreprocessor
{
    public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
    {
        foreach (FailureMessageAccessor failure in failuresAccessor.GetFailureMessages())
        {
            if (failure.GetSeverity() == FailureSeverity.Warning)
                failuresAccessor.DeleteWarning(failure);
        }
        return FailureProcessingResult.Continue;
    }
}
