using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Kanban.App.ViewModels;

namespace Kanban.App.Views;

public partial class CardView : UserControl
{
    // 이 거리(px) 이상 움직여야 드래그로 본다. 단순 클릭과 구분하기 위함.
    private const double DragThreshold = 4;

    private PointerPressedEventArgs? _pressedArgs;
    private Point _pressedPoint;

    public CardView()
    {
        InitializeComponent();

        AddHandler(PointerPressedEvent, OnPointerPressed, handledEventsToo: true);
        AddHandler(PointerMovedEvent, OnPointerMoved, handledEventsToo: true);
        AddHandler(PointerReleasedEvent, OnPointerReleased, handledEventsToo: true);

        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

    private CardViewModel? Card => DataContext as CardViewModel;

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (Card is null || Card.IsEditing)
            return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        // 카드 안의 버튼(수정, 삭제)을 누른 경우는 드래그를 시작하지 않는다.
        if (e.Source is Visual source && source.FindAncestorOfType<Button>(includeSelf: true) is not null)
            return;

        _pressedArgs = e;
        _pressedPoint = e.GetPosition(this);
    }

    private async void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_pressedArgs is null || Card is null)
            return;

        var delta = e.GetPosition(this) - _pressedPoint;
        if (Math.Abs(delta.X) < DragThreshold && Math.Abs(delta.Y) < DragThreshold)
            return;

        var args = _pressedArgs;
        _pressedArgs = null;

        // 앱 내부용 형식(Card)만 담으면 macOS가 드래그 세션을 만들지 못하고 크래시한다.
        // 플랫폼이 이해하는 텍스트 형식을 함께 담아야 한다. 같은 항목에 두 형식을 싣는다.
        var item = DataTransferItem.CreateText(Card.Title);
        item.Set(DragFormats.Card, Card);

        var transfer = new DataTransfer();
        transfer.Add(item);

        // 드래그가 끝날 때까지(드롭 또는 취소) 대기한다. 그동안 원본 카드는 흐리게 보인다.
        Root.Classes.Add("dragging");
        try
        {
            await DragDrop.DoDragDropAsync(args, transfer, DragDropEffects.Move);
        }
        finally
        {
            Root.Classes.Remove("dragging");
        }
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        var dragged = e.DataTransfer.TryGetValue(DragFormats.Card);
        if (dragged is null)
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }

        e.DragEffects = DragDropEffects.Move;
        e.Handled = true;

        // 자기 자신 위에서는 표시선을 보이지 않는다.
        if (ReferenceEquals(dragged, Card))
        {
            ClearDropIndicator();
            return;
        }

        var isLowerHalf = e.GetPosition(this).Y > Bounds.Height / 2;
        Root.Classes.Set("drop-above", !isLowerHalf);
        Root.Classes.Set("drop-below", isLowerHalf);
    }

    private void OnDragLeave(object? sender, DragEventArgs e) => ClearDropIndicator();

    private void ClearDropIndicator()
    {
        Root.Classes.Remove("drop-above");
        Root.Classes.Remove("drop-below");
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        // 누른 채로 움직이지 않고 뗐다면 "클릭"이다. 상세 패널을 연다.
        var wasClick = _pressedArgs is not null;
        _pressedArgs = null;

        if (wasClick)
            Card?.OpenDetailCommand.Execute(null);
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        ClearDropIndicator();

        var dragged = e.DataTransfer.TryGetValue(DragFormats.Card);
        if (dragged is null)
            return;

        // 카드 위의 드롭은 여기서 끝낸다. 막지 않으면 리스트의 "맨 끝에 추가"까지 실행된다.
        e.Handled = true;

        var target = Card;
        if (target?.Owner is null || ReferenceEquals(dragged, target))
            return;

        // 카드의 위쪽 절반에 놓으면 앞에, 아래쪽 절반이면 뒤에 끼워 넣는다.
        var isLowerHalf = e.GetPosition(this).Y > Bounds.Height / 2;
        var index = target.Owner.Cards.IndexOf(target) + (isLowerHalf ? 1 : 0);

        await target.Owner.MoveCardHereAsync(dragged, index);
    }
}
