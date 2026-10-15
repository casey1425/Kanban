using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kanban.Core;
using Kanban.Core.Models;

namespace Kanban.App.ViewModels;

/// <summary>보드 하나. 리스트들과, 이 보드에서만 유효한 검색/라벨 필터 상태를 가진다.</summary>
public partial class BoardViewModel : ViewModelBase
{
    private readonly IBoardRepository _repository;
    private readonly IReadOnlyList<Label> _labels;

    public int Id { get; }

    [ObservableProperty]
    private string _name;

    public ObservableCollection<BoardListViewModel> Lists { get; } = new();

    /// <summary>리스트와 카드를 이미 불러왔는지. 보드는 처음 선택될 때 한 번만 불러온다.</summary>
    public bool IsLoaded { get; private set; }

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

    public BoardViewModel(Board model, IBoardRepository repository, IReadOnlyList<Label> labels)
    {
        _repository = repository;
        _labels = labels;
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
        var list = new BoardListViewModel(model, _repository, _labels, l => Lists.Remove(l));
        list.ApplyFilter(SearchText, SelectedLabelFilter?.LabelId); // 필터가 켜진 상태에서 만든 리스트도 맞춘다.
        return list;
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
            Lists.Add(CreateListViewModel(model));
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
