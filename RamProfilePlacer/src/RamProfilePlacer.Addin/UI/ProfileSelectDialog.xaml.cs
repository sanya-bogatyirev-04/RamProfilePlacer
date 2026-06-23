using System.Windows;
using Autodesk.Revit.DB;

namespace RamProfilePlacer.Addin.UI;

/// <summary>
/// WPF-диалог выбора семейства профиля.
/// Показывает только FamilySymbol из категории «Обобщённые модели»
/// с типом размещения WorkPlaneBased.
/// </summary>
public partial class ProfileSelectDialog : Window
{
    /// <summary>Выбранный пользователем символ семейства.</summary>
    public FamilySymbol? SelectedSymbol { get; private set; }

    public ProfileSelectDialog(IEnumerable<FamilySymbol> symbols)
    {
        InitializeComponent();

        var list = symbols
            .OrderBy(s => s.Family.Name)
            .ThenBy(s => s.Name)
            .Select(s => new SymbolItem(s))
            .ToList();

        FamilyListBox.ItemsSource = list;
    }

    private void FamilyListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        OkButton.IsEnabled = FamilyListBox.SelectedItem != null;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (FamilyListBox.SelectedItem is SymbolItem item)
        {
            SelectedSymbol = item.Symbol;
            DialogResult = true;
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    /// <summary>Обёртка для отображения в ListBox.</summary>
    private sealed class SymbolItem(FamilySymbol symbol)
    {
        public FamilySymbol Symbol { get; } = symbol;

        /// <summary>Отображаемое имя: «ИмяСемейства – ИмяТипа».</summary>
        public string Name => string.IsNullOrWhiteSpace(Symbol.Name)
            ? Symbol.Family.Name
            : $"{Symbol.Family.Name} – {Symbol.Name}";
    }
}
