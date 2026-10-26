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

/// <summary>보드 하나. 리스트들과, 이 보드에서만 유효한 검색/라벨 필터 상태를 가진다.</summary>
public partial class BoardViewModel : ViewModelBase
{
    private readonly IBoardRepository _repository;
    private readonly IReadOnlyList<Label> _labels;
    private readonly Action<BoardViewModel> _onDeleted;

    public int Id { get; }

    [ObservableProperty]
    private string _name;

    public ObservableCollection<BoardListViewModel> Lists { get; } = new();

    /// <summary>리스트와 카드를 이미 불러왔는지. 보드는 처음 선택될 때 한 번만 불러온다.</summary>
    public bool IsLoaded { get; private set; }

    // ----- 이름 변경 -----

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsViewing))]
    [NotifyPropertyChangedFor(nameof(ShowDeleteButton))]
    private bool _isRenaming;

    /// <summary>이름 변경이나 삭제 확인 중이 아닌 평소 상태. 이때만 ✎ / 삭제 버튼을 보여 준다.</summary>
    public bool IsViewing => !IsRenaming && !IsConfirmingDelete;

    public bool ShowDeleteButton => IsViewing && CanDelete;

    [ObservableProperty]
    private string _editName = string.Empty;

    [RelayCommand]
    private void BeginRename()
    {
        EditName = Name;
        IsRenaming = true;
    }

    [RelayCommand]
    private Task CommitRenameAsync()
    {
        IsRenaming = false;
        var name = EditName.Trim();
        if (name.Length == 0 || name == Name)
            return Task.CompletedTask;

        Name = name;
        return RunSafeAsync(() => _repository.RenameBoardAsync(Id, name), "보드 이름을 저장하지 못했어요");
    }

    [RelayCommand]
    private void CancelRename() => IsRenaming = false;

    // ----- 삭제 (되돌릴 수 없으므로 한 번 더 확인한다) -----

    /// <summary>보드가 둘 이상일 때만 삭제할 수 있다. 마지막 보드는 남겨 둔다.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowDeleteButton))]
    private bool _canDelete;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsViewing))]
    [NotifyPropertyChangedFor(nameof(ShowDeleteButton))]
    private bool _isConfirmingDelete;

    [RelayCommand]
    private void RequestDelete() => IsConfirmingDelete = CanDelete;

    [RelayCommand]
    private void CancelDelete() => IsConfirmingDelete = false;

    [RelayCommand]
    private Task ConfirmDeleteAsync()
    {
        IsConfirmingDelete = false;
        if (!CanDelete)
            return Task.CompletedTask;

        return RunSafeAsync(async () =>
        {
            await _repository.DeleteBoardAsync(Id);
            _onDeleted(this);
        }, "보드를 삭제하지 못했어요");
    }

    // ----- 리스트 추가 -----

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddNewListCommand))]
    private string _newListName = string.Empty;

    // ----- 검색 / 라벨 필터 -----

    /// <summary>카드 검색어. 제목, 설명, 체크리스트 항목에서 찾는다.</summary>
    [ObservableProperty]
    private string _searchText = string.Empty;

    public ObservableCollection<LabelFilterOption> LabelFilters { get; } = new();

    [ObservableProperty]
    private LabelFilterOption? _selectedLabelFilter;

    public BoardViewModel(
        Board model,
        IBoardRepository repository,
        IReadOnlyList<Label> labels,
        Action<BoardViewModel> onDeleted)
    {
        _repository = repository;
        _labels = labels;
        _onDeleted = onDeleted;
        Id = model.Id;
        _name = model.Name;

        LabelFilters.Add(new LabelFilterOption(null, "모든 라벨"));
        foreach (var label in labels)
            LabelFilters.Add(new LabelFilterOption(label.Id, label.Name));
        _selectedLabelFilter = LabelFilters[0];
    }

    /// <summary>이 보드의 리스트와 카드를 불러온다. 이미 불러왔다면 아무것도 하지 않는다.</summary>
    public async Task LoadAsync()
    {
        if (IsLoaded)
            return;

        var lists = await _repository.GetListsAsync(Id);
        foreach (var list in lists)
            Lists.Add(CreateListViewModel(list));

        IsLoaded = true;
    }

    private BoardListViewModel CreateListViewModel(BoardList model)
    {
        var list = new BoardListViewModel(model, _repository, _labels, OnListDeleted);
        list.ApplyFilter(SearchText, SelectedLabelFilter?.LabelId); // 필터가 켜진 상태에서 만든 리스트도 맞춘다.
        return list;
    }

    // 리스트가 삭제(DB까지 완료)된 뒤 불린다. 화면에서 빼고 실행 취소 기록을 남긴다.
    private void OnListDeleted(BoardListViewModel list)
    {
        var index = Lists.IndexOf(list);
        Lists.Remove(list);

        if (index >= 0)
            Record(new ListPresenceAction(this, list, index, wasAdded: false));
    }

    /// <summary>리스트를 삭제하되 실행 취소 기록은 남기지 않는다(기록을 되돌리는 쪽에서 쓴다).</summary>
    internal async Task RemoveListWithoutHistoryAsync(BoardListViewModel list)
    {
        await _repository.DeleteListAsync(list.Id);
        Lists.Remove(list);
    }

    /// <summary>
    /// 삭제했던 리스트를 index 위치에 되살린다. DB에는 카드까지 같은 Id로 다시 만들고,
    /// 화면에는 같은 인스턴스를 되돌려 놓는다. 기록은 남기지 않는다.
    /// </summary>
    internal async Task RestoreListAsync(BoardListViewModel list, int index)
    {
        await _repository.RestoreListAsync(list.ToSnapshot());

        Lists.Insert(Math.Clamp(index, 0, Lists.Count), list);
        list.ApplyFilter(SearchText, SelectedLabelFilter?.LabelId); // 지운 동안 필터가 바뀌었을 수 있다.
    }

    private bool CanAddNewList() => !string.IsNullOrWhiteSpace(NewListName);

    [RelayCommand(CanExecute = nameof(CanAddNewList))]
    private Task AddNewListAsync()
    {
        var name = NewListName.Trim();
        NewListName = string.Empty;

        return RunSafeAsync(async () =>
        {
            var model = await _repository.AddListAsync(Id, name);
            var list = CreateListViewModel(model);
            Lists.Add(list);
            Record(new ListPresenceAction(this, list, Lists.Count - 1, wasAdded: true));
        }, "리스트를 추가하지 못했어요");
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnSelectedLabelFilterChanged(LabelFilterOption? value) => ApplyFilter();

    private void ApplyFilter()
    {
        foreach (var list in Lists)
            list.ApplyFilter(SearchText, SelectedLabelFilter?.LabelId);
    }

    [RelayCommand]
    private void ClearFilter()
    {
        SearchText = string.Empty;
        SelectedLabelFilter = LabelFilters.FirstOrDefault();
    }
}
