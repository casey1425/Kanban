using Avalonia.Controls;
using Avalonia.Input;
using Kanban.App.ViewModels;

namespace Kanban.App.Views;

public partial class BoardListView : UserControl
{
    public BoardListView()
    {
        InitializeComponent();

        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        var isCard = e.DataTransfer.TryGetValue(DragFormats.Card) is not null;
        e.DragEffects = isCard ? DragDropEffects.Move : DragDropEffects.None;
    }

    // 카드 위가 아닌 리스트의 빈 곳에 놓은 경우: 맨 끝에 추가한다.
    private async void OnDrop(object? sender, DragEventArgs e)
    {
        var dragged = e.DataTransfer.TryGetValue(DragFormats.Card);
        if (dragged is null || DataContext is not BoardListViewModel list)
            return;

        e.Handled = true;
        await list.MoveCardHereAsync(dragged, list.Cards.Count);
    }
}
