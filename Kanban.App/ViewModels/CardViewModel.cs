using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Kanban.App.Messages;
using Kanban.App.Undo;
using Kanban.Core;
using Kanban.Core.Models;

namespace Kanban.App.ViewModels;

public partial class CardViewModel : ViewModelBase
{
    private readonly IBoardRepository _repository;
    // 마지막으로 저장된 값. 저장할 때 "전/후"를 비교해 실행 취소 기록을 남기는 데 쓴다.
    private CardEditState _saved;

    public int Id { get; }

    [ObservableProperty]
    private string _title;

    [ObservableProperty]
    private string? _description;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDueDate))]
    [NotifyPropertyChangedFor(nameof(DueDateText))]
    [NotifyPropertyChangedFor(nameof(IsOverdue))]
    [NotifyPropertyChangedFor(nameof(DueDateTime))]
    private DateOnly? _dueDate;

    [ObservableProperty]
    private bool _isEditing;

    /// <summary>현재 검색/필터 조건에 일치하는지. false이면 화면에서 숨긴다.</summary>
    [ObservableProperty]
    private bool _isMatch = true;

    // 편집 중인 임시 값. 확정(Enter) 전에는 Title을 건드리지 않는다.
    [ObservableProperty]
    private string _editTitle = string.Empty;

    /// <summary>카드가 속한 리스트. 리스트의 Insert/Remove가 관리한다.</summary>
    public BoardListViewModel? Owner { get; internal set; }

    public bool HasDueDate => DueDate is not null;

    public string DueDateText => DueDate is { } d ? d.ToString("M/d") : string.Empty;

    public bool IsOverdue => DueDate is { } d && d < DateOnly.FromDateTime(DateTime.Today);

    /// <summary>날짜 선택 컨트롤(CalendarDatePicker.SelectedDate, DateTime?)과 연결하기 위한 변환용 속성.</summary>
    public DateTime? DueDateTime
    {
        get => DueDate?.ToDateTime(TimeOnly.MinValue);
        set => DueDate = value is { } v ? DateOnly.FromDateTime(v) : null;
    }

    /// <summary>카드에 붙은 라벨(카드 겉면에 표시).</summary>
    public ObservableCollection<LabelViewModel> Labels { get; } = new();

    public bool HasLabels => Labels.Count > 0;

    public ObservableCollection<ChecklistItemViewModel> Checklist { get; } = new();

    public bool HasChecklist => Checklist.Count > 0;

    public int ChecklistTotalCount => Checklist.Count;

    public int ChecklistDoneCount => Checklist.Count(i => i.IsDone);

    public string ChecklistSummary => $"{ChecklistDoneCount}/{ChecklistTotalCount}";

    public bool IsChecklistComplete => HasChecklist && ChecklistDoneCount == ChecklistTotalCount;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddChecklistItemCommand))]
    private string _newChecklistText = string.Empty;

    /// <summary>선택 가능한 모든 라벨 칩(상세 패널에서 켜고 끈다).</summary>
    public IReadOnlyList<LabelOptionViewModel> LabelOptions { get; }

    public CardViewModel(Card model, IBoardRepository repository, IReadOnlyList<Label> availableLabels)
    {
        _repository = repository;
        Id = model.Id;
        _title = model.Title;
        _description = model.Description;
        _dueDate = model.DueDate;
        _saved = new CardEditState(model.Title, NormalizeDescription(model.Description), model.DueDate);

        var selectedIds = model.Labels.Select(l => l.Id).ToHashSet();
        LabelOptions = availableLabels
            .Select(l => new LabelOptionViewModel(l, selectedIds.Contains(l.Id), OnLabelToggledAsync))
            .ToList();
        RefreshLabels();

        foreach (var item in model.ChecklistItems.OrderBy(i => i.Position))
            Checklist.Add(CreateChecklistItem(item));
    }

    private ChecklistItemViewModel CreateChecklistItem(ChecklistItem model) =>
        new(model, _repository, NotifyChecklistChanged, RemoveChecklistItemAsync);

    // 항목이 추가/삭제/체크될 때 요약(2/5 등)을 다시 그리게 한다.
    private void NotifyChecklistChanged()
    {
        OnPropertyChanged(nameof(HasChecklist));
        OnPropertyChanged(nameof(ChecklistTotalCount));
        OnPropertyChanged(nameof(ChecklistDoneCount));
        OnPropertyChanged(nameof(ChecklistSummary));
        OnPropertyChanged(nameof(IsChecklistComplete));
        Owner?.RefreshFilter(this); // 검색이 체크리스트 항목도 보므로 일치 여부를 다시 계산한다.
    }

    private bool CanAddChecklistItem() => !string.IsNullOrWhiteSpace(NewChecklistText);

    [RelayCommand(CanExecute = nameof(CanAddChecklistItem))]
    private Task AddChecklistItemAsync()
    {
        var text = NewChecklistText.Trim();
        NewChecklistText = string.Empty;

        return RunSafeAsync(async () =>
        {
            var model = await _repository.AddChecklistItemAsync(Id, text);
            Checklist.Add(CreateChecklistItem(model));
            NotifyChecklistChanged();
        }, "체크리스트 항목을 추가하지 못했어요");
    }

    private Task RemoveChecklistItemAsync(ChecklistItemViewModel item) => RunSafeAsync(async () =>
    {
        await _repository.DeleteChecklistItemAsync(item.Id);
        Checklist.Remove(item);
        NotifyChecklistChanged();
    }, "체크리스트 항목을 삭제하지 못했어요");

    private void RefreshLabels()
    {
        Labels.Clear();
        foreach (var option in LabelOptions.Where(o => o.IsSelected))
            Labels.Add(new LabelViewModel(new Label { Id = option.Id, Name = option.Name, Color = option.Color }));
        OnPropertyChanged(nameof(HasLabels));
    }

    private Task OnLabelToggledAsync()
    {
        RefreshLabels();
        Owner?.RefreshFilter(this);
        var ids = LabelOptions.Where(o => o.IsSelected).Select(o => o.Id).ToList();
        return RunSafeAsync(() => _repository.SetCardLabelsAsync(Id, ids), "라벨을 저장하지 못했어요");
    }

    private static string? NormalizeDescription(string? description) =>
        string.IsNullOrWhiteSpace(description) ? null : description;

    private CardEditState CurrentState() => new(Title, NormalizeDescription(Description), DueDate);

    private Card ToModel() => new()
    {
        Id = Id,
        Title = Title,
        Description = NormalizeDescription(Description),
        DueDate = DueDate,
    };

    /// <summary>
    /// 삭제된 카드를 DB에 다시 만들 때 쓰는 전체 스냅샷(라벨, 체크리스트 포함). 실행 취소용.
    /// </summary>
    internal Card ToSnapshot(int listId, int position) => new()
    {
        Id = Id,
        ListId = listId,
        Position = position,
        Title = Title,
        Description = NormalizeDescription(Description),
        DueDate = DueDate,
        Labels = LabelOptions.Where(o => o.IsSelected).Select(o => new Label { Id = o.Id }).ToList(),
        ChecklistItems = Checklist.Select((item, index) => new ChecklistItem
        {
            Id = item.Id,
            Text = item.Text,
            IsDone = item.IsDone,
            Position = index,
        }).ToList(),
    };

    /// <summary>검색어(제목, 설명, 체크리스트 항목)와 라벨 조건에 모두 일치하는지 판단한다.</summary>
    public bool Matches(string? searchText, int? labelId)
    {
        if (labelId is { } id && Labels.All(l => l.Id != id))
            return false;

        if (string.IsNullOrWhiteSpace(searchText))
            return true;

        var text = searchText.Trim();
        return Title.Contains(text, StringComparison.CurrentCultureIgnoreCase)
            || (Description?.Contains(text, StringComparison.CurrentCultureIgnoreCase) ?? false)
            || Checklist.Any(i => i.Text.Contains(text, StringComparison.CurrentCultureIgnoreCase));
    }

    /// <summary>
    /// 제목, 설명, 마감일을 저장한다. 제목을 비워 둔 경우 마지막으로 저장된 제목으로 되돌린다.
    /// 값이 실제로 바뀌었다면 실행 취소 기록을 남긴다.
    /// </summary>
    public Task SaveAsync() => RunSafeAsync(async () =>
    {
        if (string.IsNullOrWhiteSpace(Title))
            Title = _saved.Title;

        var before = _saved;
        var after = CurrentState();

        await _repository.UpdateCardAsync(ToModel());
        _saved = after;
        Owner?.RefreshFilter(this);

        if (after != before && Owner is { } list)
            Record(new EditCardAction(this, list.BoardId, before, after));
    }, "카드를 저장하지 못했어요");

    /// <summary>수정 기록을 되돌리거나 다시 실행할 때, 값을 그 상태로 맞추고 저장한다(새 기록은 남기지 않는다).</summary>
    internal async Task ApplyEditStateAsync(CardEditState state)
    {
        Title = state.Title;
        Description = state.Description;
        DueDate = state.DueDate;

        await _repository.UpdateCardAsync(ToModel());
        _saved = state;
        Owner?.RefreshFilter(this);
    }

    [RelayCommand]
    private void OpenDetail() => WeakReferenceMessenger.Default.Send(new OpenCardRequested(this));

    [RelayCommand]
    private Task DeleteAsync() => RunSafeAsync(async () =>
    {
        if (Owner is not null)
            await Owner.RemoveCardAsync(this);
    }, "카드를 삭제하지 못했어요");

    [RelayCommand]
    private void BeginEdit()
    {
        EditTitle = Title;
        IsEditing = true;
    }

    [RelayCommand]
    private async Task CommitEditAsync()
    {
        IsEditing = false;
        if (string.IsNullOrWhiteSpace(EditTitle))
            return;

        Title = EditTitle.Trim();
        await SaveAsync();
    }

    [RelayCommand]
    private void CancelEdit() => IsEditing = false;
}
