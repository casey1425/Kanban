using Avalonia.Controls;
using Avalonia.Input;
using Kanban.App.ViewModels;

namespace Kanban.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnBackdropPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            vm.CloseDetailCommand.Execute(null);
    }
}