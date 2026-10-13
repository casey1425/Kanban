using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kanban.Core;
using Kanban.Core.Models;

namespace Kanban.App.ViewModels;

public partial class BoardListViewModel : ViewModelBase
{
    private readonly IBoardRepository _repository;
    private readonly Action<BoardListViewModel> _onDeleted;
    private readonly IReadOnlyList<Label> _labels;

    public int Id { get; }

    [ObservableProperty]
    private string _name;

    // 입력창과 양방향 바인딩된다. 값이 바뀌면 추가 버튼의 활성 상태도 다시 계산한다.
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddNewCardCommand))]
    private string _newCardTitle = string.Empty;

    public ObservableCollection<CardViewModel> Cards { get; } = new();

    public BoardListViewModel(
        BoardList model,
        IBoardRepository repository,
        IReadOnlyList<Label> labels,
        Action<BoardListViewModel> onDeleted)
    {
        _repository = repository;
        _labels = labels;
        _onDeleted = onDeleted;
        Id = model.Id;
        _name = model.Name;

        foreach (var card in model.Cards.OrderBy(c => c.Position))
            AddCardFromModel(card);
    }

    private void AddCardFromModel(Card model)
    {
        var card = new CardViewModel(model, _repository, _labels) { Owner = this };
        Cards.Add(card);
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        await _repository.DeleteListAsync(Id);
        _onDeleted(this);
    }

    private bool CanAddNewCard() => !string.IsNullOrWhiteSpace(NewCardTitle);

    [RelayCommand(CanExecute = nameof(CanAddNewCard))]
    private async Task AddNewCardAsync()
    {
        var title = NewCardTitle.Trim();
        NewCardTitle = string.Empty;

        var model = await _repository.AddCardAsync(Id, title, description: null);
        AddCardFromModel(model);
    }

    public async Task RemoveCardAsync(CardViewModel card)
    {
        await _repository.DeleteCardAsync(card.Id);
        RemoveCard(card);
    }

    /// <summary>
    /// 카드를 이 리스트의 index 위치로 옮기고(화면), 영향받은 리스트의 순서를 저장한다(DB).
    /// 화면 먼저 바꾸므로 드롭 직후 바로 반영된다.
    /// </summary>
    public async Task MoveCardHereAsync(CardViewModel card, int index)
    {
        var source = card.Owner;
        InsertCard(card, index);

        var orders = new List<ListCardOrder> { OrderOf(this) };
        if (source is not null && source != this)
            orders.Add(OrderOf(source));

        await _repository.SaveCardOrderAsync(orders);
    }

    private static ListCardOrder OrderOf(BoardListViewModel list) =>
        new(list.Id, list.Cards.Select(c => c.Id).ToList());

    /// <summary>
    /// 카드를 이 리스트의 index 위치에 넣는다(화면 상태만 바꾼다).
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
}
