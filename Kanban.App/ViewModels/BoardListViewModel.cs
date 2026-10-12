using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Kanban.App.ViewModels;

public partial class BoardListViewModel : ViewModelBase
{
    private readonly Action<BoardListViewModel> _onDelete;

    [ObservableProperty]
    private string _name;

    // 입력창과 양방향 바인딩된다. 값이 바뀌면 추가 버튼의 활성 상태도 다시 계산한다.
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddNewCardCommand))]
    private string _newCardTitle = string.Empty;

    public ObservableCollection<CardViewModel> Cards { get; } = new();

    public BoardListViewModel(string name, Action<BoardListViewModel> onDelete)
    {
        _name = name;
        _onDelete = onDelete;
    }

    [RelayCommand]
    private void Delete() => _onDelete(this);

    public void AddCard(string title, string? description = null)
    {
        InsertCard(new CardViewModel(title, description), Cards.Count);
    }

    /// <summary>
    /// 카드를 이 리스트의 index 위치에 넣는다.
    /// 다른 리스트에 있던 카드면 그쪽에서 빼 오고, 같은 리스트면 순서만 바꾼다.
    /// index는 "이동 전" 기준 삽입 위치이다. 범위를 벗어나면 보정한다.
    /// </summary>
    public void InsertCard(CardViewModel card, int index)
    {
        if (card.Owner == this)
        {
            var oldIndex = Cards.IndexOf(card);
            if (oldIndex < index)
                index--; // 자기 자신이 빠지면서 뒤쪽 위치가 한 칸 당겨진다.

            index = Math.Clamp(index, 0, Cards.Count - 1);
            if (index != oldIndex)
                Cards.Move(oldIndex, index);
            return;
        }

        card.Owner?.RemoveCard(card);
        Cards.Insert(Math.Clamp(index, 0, Cards.Count), card);
        card.Owner = this;
    }

    public void RemoveCard(CardViewModel card)
    {
        if (Cards.Remove(card))
            card.Owner = null;
    }

    private bool CanAddNewCard() => !string.IsNullOrWhiteSpace(NewCardTitle);

    [RelayCommand(CanExecute = nameof(CanAddNewCard))]
    private void AddNewCard()
    {
        AddCard(NewCardTitle.Trim());
        NewCardTitle = string.Empty;
    }
}
