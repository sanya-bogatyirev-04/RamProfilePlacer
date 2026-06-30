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

        UIDocument uidoc = commandData.Application.ActiveUIDocument;
        Document doc = uidoc.Document;

        try
        {
            // 1. Собираем доступные семейства профилей
            var symbols = CollectProfileSymbols(doc);
            if (symbols.Count == 0)
            {
                TaskDialog.Show("РАМ — Профили",
                    "В модели не загружено ни одного семейства категории «Обобщённые модели» " +
                    "с типом размещения WorkPlaneBased.\n\nЗагрузите семейство профиля и повторите.");
                logger.Warn("Подходящие символы семейств не найдены.");
                return Result.Cancelled;
            }

            // 2. Диалог выбора семейства
            var dialog = new ProfileSelectDialog(symbols);
            // Привязываем к окну Revit, чтобы диалог не уходил за него
            var helper = new System.Windows.Interop.WindowInteropHelper(dialog);
            helper.Owner = commandData.Application.MainWindowHandle;
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
            .Cast<FamilySymbol>()
            .Where(s =>
                s.Category?.Id.Value == (long)BuiltInCategory.OST_GenericModel &&
                s.Family.FamilyPlacementType == FamilyPlacementType.WorkPlaneBased)
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

        // Подавляем предупреждения Revit
        var failOpts = tx.GetFailureHandlingOptions();
        failOpts.SetFailuresPreprocessor(new WarningSwallower());
        tx.SetFailureHandlingOptions(failOpts);

        foreach (var r in refs)
        {
            var opening = doc.GetElement(r) as FamilyInstance;
            if (opening == null)
            {
                report.AddSkipped(r.ElementId, "Элемент не является FamilyInstance.");
                continue;
            }

            logger.Info($"Обработка проёма ElementId={opening.Id}, категория={opening.Category?.Name}");

            try
            {
                int placed = PlaceProfilesForOpening(doc, opening, profileSymbol, logger);
                report.AddPlaced(opening.Id, placed);
                logger.Info($"  → Размещено профилей: {placed} для ElementId={opening.Id}.");
            }
            catch (NotSupportedException ex)
            {
                // Дуговая стена и т.п. — пропускаем, не прерываем обработку
                report.AddSkipped(opening.Id, ex.Message);
                logger.Warn($"  → Пропущен ElementId={opening.Id}: {ex.Message}");
            }
            catch (Exception ex)
            {
                report.AddSkipped(opening.Id, $"Ошибка: {ex.Message}");
                logger.Error($"  → Ошибка при обработке ElementId={opening.Id}.", ex);
            }
        }

        var status = tx.Commit();
        if (status != TransactionStatus.Committed)
        {
            // После неудачного Commit Revit автоматически откатывает транзакцию.
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
        // Получаем родительскую стену
        var wall = opening.Host as Wall
            ?? throw new InvalidOperationException($"Opening {opening.Id} has no Wall host.");

        // Проверяем, что стена прямая (не дуговая)
        if (wall.Location is not LocationCurve lc || lc.Curve is not Line)
            throw new NotSupportedException("Стена дуговая — размещение профилей не поддерживается, проём пропущен.");

        // Внутренняя грань и её Reference
        var (face, faceRef) = WallFaceReader.GetInteriorFace(wall);

        // Точка вставки проёма
        XYZ openingLocation = (opening.Location as LocationPoint)?.Point
            ?? throw new InvalidOperationException($"Cannot get location point for opening {opening.Id}.");

        // Размеры внутреннего проёма
        var dims = WallFaceReader.GetOpeningDimensions(face, openingLocation, opening, out bool usedFallback, logger);

        if (usedFallback)
        {
            logger.Warn($"  Для ElementId={opening.Id} использован запасной путь. " +
                        "Размеры взяты из параметров семейства — рекомендуется проверить вручную.");
        }

        // Тип: дверь или окно?
        bool isDoor = opening.Category?.Id.Value == (long)BuiltInCategory.OST_Doors;

        // Раскладка профилей (Core)
        var placements = ProfileLayoutCalculator.Calculate(dims, isDoor);

        // Размещение каждого профиля
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
