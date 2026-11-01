using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kanban.App.Undo;
using Kanban.Core;
using Kanban.Core.Models;

namespace Kanban.App.ViewModels;

public partial class BoardListViewModel : ViewModelBase
{
    private readonly IBoardRepository _repository;
    private readonly Action<BoardListViewModel> _onDeleted;
    private readonly IReadOnlyList<Label> _labels;
    private string? _filterText;
    private int? _filterLabelId;

    public int Id { get; }

    /// <summary>이 리스트가 속한 보드. 실행 취소 때 어느 보드로 이동할지 알기 위해 가진다.</summary>
    public int BoardId { get; }

    /// <summary>보드 안에서의 순서값(DB의 Position). 삭제한 리스트를 복원할 때 원래 자리를 지키는 데 쓴다.</summary>
    public int Position { get; }

    [ObservableProperty]
    private string _name;

    /// <summary>완료 리스트인지. 이 리스트의 카드는 "완료"로 집계된다.</summary>
    [ObservableProperty]
    private bool _isDoneList;

    /// <summary>동시 진행 제한(WIP limit). null이면 제한 없음. 넘으면 경고만 하고 막지는 않는다.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasWipLimit))]
    [NotifyPropertyChangedFor(nameof(IsOverWipLimit))]
    [NotifyPropertyChangedFor(nameof(CardCountText))]
    private int? _wipLimit;

    public bool HasWipLimit => WipLimit is not null;

    /// <summary>카드 수가 WIP 제한을 넘었는지. 필터로 숨겨진 카드도 센다(실제로 진행 중인 일의 수이므로).</summary>
    public bool IsOverWipLimit => WipLimit is { } limit && Cards.Count > limit;

    /// <summary>헤더에 보여 줄 카드 수. 제한이 있으면 "3 / 5", 없으면 "3".</summary>
    public string CardCountText => WipLimit is { } limit ? $"{Cards.Count} / {limit}" : Cards.Count.ToString();

    private void NotifyCardCountChanged()
    {
        OnPropertyChanged(nameof(IsOverWipLimit));
        OnPropertyChanged(nameof(CardCountText));
    }

    // ----- 설정 편집(초안을 고치다가 저장하면 한 번에 반영) -----

    [ObservableProperty]
    private bool _isSettingsOpen;

    [ObservableProperty]
    private string _editName = string.Empty;

    [ObservableProperty]
    private bool _editIsDoneList;

    // NumericUpDown.Value가 decimal?라서 같은 타입으로 둔다. 저장할 때 int?로 바꾼다.
    [ObservableProperty]
    private decimal? _editWipLimit;

    private ListSettings CurrentSettings() => new(Name, IsDoneList, WipLimit);

    [RelayCommand]
    private void ToggleSettings()
    {
        if (IsSettingsOpen)
        {
            IsSettingsOpen = false;
            return;
        }

        EditName = Name;
        EditIsDoneList = IsDoneList;
        EditWipLimit = WipLimit;
        IsSettingsOpen = true;
    }

    [RelayCommand]
    private void CancelSettings() => IsSettingsOpen = false;

    [RelayCommand]
    private void ClearWipLimit() => EditWipLimit = null;

    [RelayCommand]
    private Task SaveSettingsAsync()
    {
        IsSettingsOpen = false;

        var name = string.IsNullOrWhiteSpace(EditName) ? Name : EditName.Trim();
        // 비어 있거나 1 미만이면 "제한 없음". 터무니없이 큰 값은 999로 맞춘다.
        int? limit = EditWipLimit is { } value && value >= 1 ? (int)Math.Min(value, 999) : null;

        var before = CurrentSettings();
        var after = new ListSettings(name, EditIsDoneList, limit);
        if (after == before)
            return Task.CompletedTask;

        return RunSafeAsync(async () =>
        {
            await ApplySettingsAsync(after);
            Record(new EditListSettingsAction(this, before, after));
        }, "리스트 설정을 저장하지 못했어요");
    }

    /// <summary>설정을 저장하고 화면에 반영한다. 저장이 실패하면 화면은 바뀌지 않는다. 기록은 남기지 않는다.</summary>
    internal async Task ApplySettingsAsync(ListSettings settings)
    {
        await _repository.UpdateListSettingsAsync(Id, settings.Name, settings.IsDoneList, settings.WipLimit);

        Name = settings.Name;
        IsDoneList = settings.IsDoneList;
        WipLimit = settings.WipLimit;
    }

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
        BoardId = model.BoardId;
        Position = model.Position;
        _name = model.Name;
        _isDoneList = model.IsDoneList;
        _wipLimit = model.WipLimit;

        foreach (var card in model.Cards.OrderBy(c => c.Position))
            AddCardFromModel(card);

        // 카드가 추가·삭제·이동될 때마다 헤더의 카드 수와 제한 초과 경고를 갱신한다.
        Cards.CollectionChanged += (_, _) => NotifyCardCountChanged();
    }

    /// <summary>삭제한 리스트를 DB에 같은 Id로 다시 만들 때 쓰는 전체 스냅샷(카드, 라벨, 체크리스트 포함). 실행 취소용.</summary>
    internal BoardList ToSnapshot() => new()
    {
        Id = Id,
        BoardId = BoardId,
        Name = Name,
        Position = Position,
        IsDoneList = IsDoneList,
        WipLimit = WipLimit,
        Cards = Cards.Select((card, index) => card.ToSnapshot(Id, index)).ToList(),
    };

    private CardViewModel AddCardFromModel(Card model)
    {
        var card = new CardViewModel(model, _repository, _labels) { Owner = this };
        card.IsMatch = card.Matches(_filterText, _filterLabelId);
        Cards.Add(card);
        return card;
    }

    /// <summary>검색/라벨 조건을 저장하고, 이 리스트의 모든 카드에 적용한다.</summary>
    public void ApplyFilter(string? searchText, int? labelId)
    {
        _filterText = searchText;
        _filterLabelId = labelId;

        foreach (var card in Cards)
            RefreshFilter(card);
    }

    /// <summary>카드의 제목이나 라벨이 바뀐 뒤, 현재 조건에 다시 맞춰 본다.</summary>
    public void RefreshFilter(CardViewModel card) =>
        card.IsMatch = card.Matches(_filterText, _filterLabelId);

    [RelayCommand]
    private Task DeleteAsync() => RunSafeAsync(async () =>
    {
        await _repository.DeleteListAsync(Id);
        _onDeleted(this);
    }, "리스트를 삭제하지 못했어요");

    private bool CanAddNewCard() => !string.IsNullOrWhiteSpace(NewCardTitle);

    [RelayCommand(CanExecute = nameof(CanAddNewCard))]
    private Task AddNewCardAsync()
    {
        var title = NewCardTitle.Trim();
        NewCardTitle = string.Empty;

        return RunSafeAsync(async () =>
        {
            var model = await _repository.AddCardAsync(Id, title, description: null);
            var card = AddCardFromModel(model);
            Record(new CardPresenceAction(card, this, Cards.Count - 1, wasAdded: true));
        }, "카드를 추가하지 못했어요");
    }

    /// <summary>카드를 삭제한다(DB와 화면). 실행 취소 기록을 남긴다.</summary>
    public async Task RemoveCardAsync(CardViewModel card)
    {
        var index = Cards.IndexOf(card);
        await RemoveCardWithoutHistoryAsync(card);

        if (index >= 0)
            Record(new CardPresenceAction(card, this, index, wasAdded: false));
    }

    /// <summary>카드를 삭제하되 실행 취소 기록은 남기지 않는다(기록을 되돌리는 쪽에서 쓴다).</summary>
    internal async Task RemoveCardWithoutHistoryAsync(CardViewModel card)
    {
        await _repository.DeleteCardAsync(card.Id);
        RemoveCard(card);
    }

    /// <summary>
    /// 삭제했던 카드를 index 위치에 되살린다. DB에는 같은 Id로 다시 만들고, 화면에는 같은 인스턴스를 되돌려 놓는다.
    /// 기록은 남기지 않는다.
    /// </summary>
    internal async Task RestoreCardAsync(CardViewModel card, int index)
    {
        index = Math.Clamp(index, 0, Cards.Count);

        await _repository.RestoreCardAsync(card.ToSnapshot(Id, index));

        Cards.Insert(index, card);
        card.Owner = this;
        RefreshFilter(card);

        // 복원한 카드의 Position이 다른 카드와 겹칠 수 있으므로, 순서를 한 번 정리해 저장한다.
        await _repository.SaveCardOrderAsync(new[] { OrderOf(this) });
    }

    /// <summary>
    /// 카드를 이 리스트의 index 위치로 옮기고(화면), 영향받은 리스트의 순서를 저장한다(DB).
    /// 화면 먼저 바꾸므로 드롭 직후 바로 반영된다.
    /// </summary>
    public Task MoveCardHereAsync(CardViewModel card, int index) => TryMoveCardHereAsync(card, index);

    /// <summary>MoveCardHereAsync와 같지만, 저장까지 성공했는지 알려 준다. 성공하면 실행 취소 기록을 남긴다.</summary>
    public async Task<bool> TryMoveCardHereAsync(CardViewModel card, int index)
    {
        var source = card.Owner;
        var fromIndex = source?.Cards.IndexOf(card) ?? -1;

        InsertCard(card, index);
        var toIndex = Cards.IndexOf(card);

        var saved = await TryRunAsync(() => SaveOrdersAsync(source), "카드 위치를 저장하지 못했어요");

        // 제자리에 다시 놓은 경우는 되돌릴 일이 없다.
        var moved = source is not null && !(source == this && fromIndex == toIndex);
        if (saved && moved)
            Record(new MoveCardAction(card, source!, fromIndex, this, toIndex));

        return saved;
    }

    /// <summary>
    /// 카드를 이 리스트의 "최종 위치" index에 놓고 저장한다. 기록은 남기지 않는다.
    /// (InsertCard의 index는 이동 전 기준이라 같은 리스트에서는 해석이 다르다. 여기서는 이동 후 위치다.)
    /// </summary>
    internal async Task PlaceCardAsync(CardViewModel card, int finalIndex)
    {
        var source = card.Owner;
        PlaceCardAt(card, finalIndex);
        await SaveOrdersAsync(source);
    }

    private async Task SaveOrdersAsync(BoardListViewModel? source)
    {
        var orders = new List<ListCardOrder> { OrderOf(this) };
        if (source is not null && source != this)
            orders.Add(OrderOf(source));

        await _repository.SaveCardOrderAsync(orders);
    }

    private static ListCardOrder OrderOf(BoardListViewModel list) =>
        new(list.Id, list.Cards.Select(c => c.Id).ToList());

    private void PlaceCardAt(CardViewModel card, int finalIndex)
    {
        if (card.Owner == this)
        {
            var oldIndex = Cards.IndexOf(card);
            var index = Math.Clamp(finalIndex, 0, Cards.Count - 1);
            if (index != oldIndex)
                Cards.Move(oldIndex, index);
            return;
        }

        card.Owner?.RemoveCard(card);
        Cards.Insert(Math.Clamp(finalIndex, 0, Cards.Count), card);
        card.Owner = this;
        RefreshFilter(card);
    }

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

            PlaceCardAt(card, index);
            return;
        }

        PlaceCardAt(card, index);
    }

    public void RemoveCard(CardViewModel card)
    {
        if (Cards.Remove(card))
            card.Owner = null;
    }
}
