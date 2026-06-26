using Autodesk.Revit.UI;
using RamProfilePlacer.Addin.Commands;

namespace RamProfilePlacer.Addin;

/// <summary>
/// Точка входа плагина. Регистрирует кнопку на ленте Revit.
/// </summary>
public sealed class Application : IExternalApplication
{
    private const string TabName = "РАМ Инжиниринг";
    private const string PanelName = "Профили";
    private const string ButtonName = "Разместить\nпрофили";
    private const string ButtonTooltip =
        "Автоматически размещает профильные изделия (откосы, наличники) " +
        "по внутреннему периметру выбранных оконных и дверных проёмов.";

    public Result OnStartup(UIControlledApplication application)
    {
        try
        {
            // Вкладка (CreateRibbonTab бросает ArgumentException, если вкладка уже существует)
            try { application.CreateRibbonTab(TabName); }
            catch (Autodesk.Revit.Exceptions.ArgumentException) { }

            // Панель
            RibbonPanel panel = GetOrCreatePanel(application, TabName, PanelName);

            // Кнопка
            string addinPath = typeof(Application).Assembly.Location;

            var pushButtonData = new PushButtonData(
                nameof(PlaceProfilesCommand),
                ButtonName,
                addinPath,
                typeof(PlaceProfilesCommand).FullName!)
            {
                ToolTip = ButtonTooltip,
                LongDescription =
                    "Шаг 1: выберите семейство профиля из списка.\n" +
                    "Шаг 2: выберите окна и/или двери в модели.\n" +
                    "Шаг 3: профили автоматически размещаются на внутренней грани стены.",
            };

            panel.AddItem(pushButtonData);
            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            Infrastructure.FileLogger.Instance.Error("Ошибка при запуске плагина (OnStartup).", ex);
            return Result.Failed;
        }
    }

    public Result OnShutdown(UIControlledApplication application)
    {
        Infrastructure.FileLogger.Instance.Info("Плагин выгружен.");
        return Result.Succeeded;
    }

    private static RibbonPanel GetOrCreatePanel(UIControlledApplication app, string tab, string panelName)
    {
        var panels = app.GetRibbonPanels(tab);
        return panels.FirstOrDefault(p => p.Name == panelName)
               ?? app.CreateRibbonPanel(tab, panelName);
    }
}
