using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using RamProfilePlacer.Addin.Infrastructure;
using RamProfilePlacer.Addin.Revit;
using RamProfilePlacer.Addin.UI;
using RamProfilePlacer.Core;

namespace RamProfilePlacer.Addin.Commands;

[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public sealed class PlaceProfilesCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var logger = FileLogger.Instance;
        logger.Info("=== Команда PlaceProfilesCommand запущена ===");

        UIApplication uiApp = commandData.Application;
        UIDocument? uidoc = uiApp.ActiveUIDocument;
        if (uidoc == null)
        {
            TaskDialog.Show("РАМ — Профили", "Нет открытого документа.");
            return Result.Cancelled;
        }

        Document doc = uidoc.Document;

        try
        {
            // 1. Собираем доступные семейства профилей (нативная фильтрация по категории)
            var symbols = CollectProfileSymbols(doc);
            if (symbols.Count == 0)
            {
                TaskDialog.Show("РАМ — Профили",
                    "В модели не загружено ни одного семейства категории «Обобщённые модели» " +
                    "с типом размещения WorkPlaneBased.\n\nЗагрузите семейство профиля и повторите.");
                logger.Warn("Подходящие символы семейств не найдены.");
                return Result.Cancelled;
            }

            // 2. Диалог выбора семейства (с владельцем = окно Revit)
            var dialog = new ProfileSelectDialog(symbols);
            new WindowInteropHelper(dialog).Owner = uiApp.MainWindowHandle;
            if (dialog.ShowDialog() != true || dialog.SelectedSymbol == null)
            {
                logger.Info("Пользователь отменил выбор семейства.");
                return Result.Cancelled;
            }

            FamilySymbol profileSymbol = dialog.SelectedSymbol;
            logger.Info($"Выбрано семейство: '{profileSymbol.Family.Name}', тип: '{profileSymbol.Name}' " +
                        $"(ElementId={profileSymbol.Id})");

            // 3. Выбор проёмов в модели
            IList<Reference> selectedRefs;
            try
            {
                selectedRefs = uidoc.Selection.PickObjects(
                    ObjectType.Element,
                    new OpeningSelectionFilter(),
                    "Выберите окна и/или двери, затем нажмите Enter");
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                logger.Info("Пользователь отменил выбор проёмов.");
                return Result.Cancelled;
            }

            if (selectedRefs.Count == 0)
            {
                logger.Info("Проёмы не выбраны.");
                return Result.Cancelled;
            }

            logger.Info($"Выбрано проёмов: {selectedRefs.Count}.");

            // 4. Обработка проёмов и размещение профилей
            var report = ProcessOpenings(doc, selectedRefs, profileSymbol, logger);

            // 5. Итоговый отчёт
            logger.Info($"=== Команда завершена: размещено={report.PlacedCount}, пропущено={report.SkippedCount} ===");

            string reportText = BuildReportText(report);
            TaskDialog.Show("РАМ — Профили: результат", reportText);

            return report.PlacedCount > 0 ? Result.Succeeded : Result.Cancelled;
        }
        catch (Exception ex)
        {
            logger.Error("Необработанное исключение в PlaceProfilesCommand.", ex);
            message = ex.Message;
            return Result.Failed;
        }
    }

    // -------------------------------------------------------------------------

    private static List<FamilySymbol> CollectProfileSymbols(Document doc)
    {
        return new FilteredElementCollector(doc)
            .OfClass(typeof(FamilySymbol))
            .OfCategory(BuiltInCategory.OST_GenericModel)
            .Cast<FamilySymbol>()
            .Where(s => s.Family.FamilyPlacementType == FamilyPlacementType.WorkPlaneBased)
            .ToList();
    }

    private static PlacementReport ProcessOpenings(
        Document doc,
        IList<Reference> refs,
        FamilySymbol profileSymbol,
        FileLogger logger)
    {
        var report = new PlacementReport();

        using var tx = new Transaction(doc, "Размещение профилей (РАМ)");
        tx.Start();

        var failOpts = tx.GetFailureHandlingOptions();
        failOpts.SetFailuresPreprocessor(new WarningSwallower());
        tx.SetFailureHandlingOptions(failOpts);

        // Активируем символ один раз до цикла + Regenerate (best practice Revit API)
        if (!profileSymbol.IsActive)
        {
            profileSymbol.Activate();
            doc.Regenerate();
        }

        foreach (var r in refs)
        {
            var opening = doc.GetElement(r) as FamilyInstance;
            if (opening == null)
            {
                report.AddSkipped(r.ElementId, "Элемент не является FamilyInstance.");
                continue;
            }

            logger.Info($"Обработка проёма ElementId={opening.Id}, категория={opening.Category?.Name}");

            // SubTransaction на каждый проём: при сбое откатываются только профили этого проёма
            using var sub = new SubTransaction(doc);
            sub.Start();

            try
            {
                int placed = PlaceProfilesForOpening(doc, opening, profileSymbol, logger);

                if (placed == 0)
                {
                    sub.RollBack();
                    report.AddSkipped(opening.Id, "Не удалось разместить ни одного профиля (NewFamilyInstance вернул null).");
                    logger.Warn($"  → 0 профилей размещено для ElementId={opening.Id}, откат.");
                }
                else
                {
                    sub.Commit();
                    report.AddPlaced(opening.Id, placed);
                    logger.Info($"  → Размещено профилей: {placed} для ElementId={opening.Id}.");
                }
            }
            catch (NotSupportedException ex)
            {
                sub.RollBack();
                report.AddSkipped(opening.Id, ex.Message);
                logger.Warn($"  → Пропущен ElementId={opening.Id}: {ex.Message}");
            }
            catch (Exception ex)
            {
                sub.RollBack();
                report.AddSkipped(opening.Id, $"Ошибка: {ex.Message}");
                logger.Error($"  → Ошибка при обработке ElementId={opening.Id}.", ex);
            }
        }

        var status = tx.Commit();
        if (status != TransactionStatus.Committed)
        {
            logger.Error($"Транзакция не зафиксирована: status={status}.");
            throw new InvalidOperationException("Не удалось сохранить изменения, операция отменена.");
        }

        return report;
    }

    private static int PlaceProfilesForOpening(
        Document doc,
        FamilyInstance opening,
        FamilySymbol profileSymbol,
        FileLogger logger)
    {
        var wall = opening.Host as Wall
            ?? throw new InvalidOperationException($"Opening {opening.Id} has no Wall host.");

        if (wall.Location is not LocationCurve lc || lc.Curve is not Line)
            throw new NotSupportedException("Стена дуговая — размещение профилей не поддерживается, проём пропущен.");

        var (face, faceRef) = WallFaceReader.GetInteriorFace(wall);

        XYZ openingLocation = (opening.Location as LocationPoint)?.Point
            ?? throw new InvalidOperationException($"Cannot get location point for opening {opening.Id}.");

        var dims = WallFaceReader.GetOpeningDimensions(face, openingLocation, opening, out bool usedFallback, logger);

        if (usedFallback)
        {
            logger.Warn($"  Для ElementId={opening.Id} использован запасной путь. " +
                        "Размеры взяты из параметров семейства — рекомендуется проверить вручную.");
        }

        bool isDoor = opening.Category?.BuiltInCategory == BuiltInCategory.OST_Doors;

        var placements = ProfileLayoutCalculator.Calculate(dims, isDoor);

        int placed = 0;
        foreach (var placement in placements)
        {
            var fi = FamilyPlacer.PlaceProfile(doc, profileSymbol, faceRef, placement);
            if (fi != null) placed++;
        }

        return placed;
    }

    private static string BuildReportText(PlacementReport report)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Размещено профилей: {report.PlacedCount}");

        if (report.SkippedCount > 0)
        {
            sb.AppendLine($"Пропущено проёмов: {report.SkippedCount}");
            sb.AppendLine();
            sb.AppendLine("Причины пропуска:");
            foreach (var (id, reason) in report.SkippedItems)
                sb.AppendLine($"  • ElementId {id}: {reason}");
        }

        return sb.ToString();
    }

    // -------------------------------------------------------------------------

    private sealed class PlacementReport
    {
        public int PlacedCount { get; private set; }
        public int SkippedCount { get; private set; }
        public List<(ElementId Id, string Reason)> SkippedItems { get; } = [];

        public void AddPlaced(ElementId id, int count) => PlacedCount += count;

        public void AddSkipped(ElementId id, string reason)
        {
            SkippedCount++;
            SkippedItems.Add((id, reason));
        }
    }
}
