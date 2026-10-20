using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Kanban.App.ViewModels;

namespace Kanban.App.Views;

public partial class MainWindow : Window
{
    private ListBoxItem? _dropTarget;

    public MainWindow()
    {
        InitializeComponent();

        // 사이드바의 보드 위로 카드를 끌어다 놓으면 그 보드로 옮긴다.
        BoardList.AddHandler(DragDrop.DragOverEvent, OnBoardDragOver);
        BoardList.AddHandler(DragDrop.DragLeaveEvent, OnBoardDragLeave);
        BoardList.AddHandler(DragDrop.DropEvent, OnBoardDrop);
    }

    private void OnBackdropPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            vm.CloseDetailCommand.Execute(null);
    }

    private static ListBoxItem? ItemAt(DragEventArgs e) =>
        (e.Source as Avalonia.Visual)?.FindAncestorOfType<ListBoxItem>(includeSelf: true);

    private void OnBoardDragOver(object? sender, DragEventArgs e)
    {
        ClearDropTarget();

        var card = e.DataTransfer.TryGetValue(DragFormats.Card);
        var item = ItemAt(e);

        // 카드가 아니거나, 보드 항목이 아니거나, 이미 이 보드에 있는 카드면 놓을 수 없다.
        if (card?.Owner is null || item?.DataContext is not BoardViewModel board || board.Lists.Contains(card.Owner))
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }

        e.DragEffects = DragDropEffects.Move;
        e.Handled = true;

        item.Classes.Add("drop-target");
        _dropTarget = item;
    }

    private void OnBoardDragLeave(object? sender, DragEventArgs e) => ClearDropTarget();

    private async void OnBoardDrop(object? sender, DragEventArgs e)
    {
        ClearDropTarget();

        var card = e.DataTransfer.TryGetValue(DragFormats.Card);
        if (card is null || ItemAt(e)?.DataContext is not BoardViewModel board || DataContext is not MainWindowViewModel vm)
            return;

        e.Handled = true;
        await vm.MoveCardToBoardAsync(card, board);
    }

    private void ClearDropTarget()
    {
        _dropTarget?.Classes.Remove("drop-target");
        _dropTarget = null;
    }
}
