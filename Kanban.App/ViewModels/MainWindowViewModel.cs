using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Kanban.App.Messages;
using Kanban.Core;
using Kanban.App.Services;
using Kanban.Core.Models;

namespace Kanban.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IBoardRepository _repository;
    private readonly ISettingsService _settings;
    private IReadOnlyList<Label> _labels = Array.Empty<Label>();
    private int _boardId;

    public string BoardTitle { get; } = "My Kanban Board";

    public ObservableCollection<BoardListViewModel> Lists { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddNewListCommand))]
    private string _newListName = string.Empty;

    /// <summary>카드 검색어. 제목, 설명, 체크리스트 항목에서 찾는다.</summary>
    [ObservableProperty]
    private string _searchText = string.Empty;

    public ObservableCollection<LabelFilterOption> LabelFilters { get; } = new();

    [ObservableProperty]
    private LabelFilterOption? _selectedLabelFilter;

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

    /// <summary>불러오기/저장 실패 시 화면 상단에 보여 줄 메시지. 사용자가 닫을 수 있다.</summary>
    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>열려 있는 카드 상세 패널. null이면 닫혀 있다.</summary>
    [ObservableProperty]
    private CardDetailViewModel? _detail;

    /// <summary>현재 테마 모드. 바뀌면 설정 파일에 저장하고, 앱(App.axaml.cs)이 실제 테마를 바꾼다.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ThemeLabel))]
    private ThemeMode _themeMode;

    public string ThemeLabel => ThemeMode switch
    {
        ThemeMode.Light => "☀ 라이트",
        ThemeMode.Dark => "🌙 다크",
        _ => "🖥 시스템",
    };

    partial void OnThemeModeChanged(ThemeMode value)
    {
        _settings.Current.Theme = value;
        try
        {
            _settings.Save();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"설정을 저장하지 못했어요: {ex.Message}";
        }
    }

    [RelayCommand]
    private void CycleTheme() => ThemeMode = ThemeMode switch
    {
        ThemeMode.System => ThemeMode.Light,
        ThemeMode.Light => ThemeMode.Dark,
        _ => ThemeMode.System,
    };

    public MainWindowViewModel(IBoardRepository repository, ISettingsService settings)
    {
        _repository = repository;
        _settings = settings;
        _themeMode = settings.Current.Theme;

        WeakReferenceMessenger.Default.Register<MainWindowViewModel, OpenCardRequested>(
            this, (vm, message) => vm.Detail = new CardDetailViewModel(message.Card, () => vm.Detail = null));

        WeakReferenceMessenger.Default.Register<MainWindowViewModel, ErrorOccurred>(
            this, (vm, message) => vm.ErrorMessage = message.Message);
    }

    [RelayCommand]
    private void DismissError() => ErrorMessage = null;

    [RelayCommand]
    private void CloseDetail() => Detail?.CloseCommand.Execute(null);

    /// <summary>DB를 준비하고 저장된 보드를 불러온다. 처음 실행이면 기본 리스트를 만든다.</summary>
    public async Task InitializeAsync()
    {
        try
        {
            await _repository.InitializeAsync();

            _labels = await _repository.GetLabelsAsync();

            LabelFilters.Add(new LabelFilterOption(null, "모든 라벨"));
            foreach (var label in _labels)
                LabelFilters.Add(new LabelFilterOption(label.Id, label.Name));
            SelectedLabelFilter = LabelFilters[0];

            var boards = await _repository.GetBoardsAsync();
            _boardId = boards.Count > 0 ? boards[0].Id : (await _repository.AddBoardAsync("My Kanban Board")).Id;

            var lists = await _repository.GetListsAsync(_boardId);
            if (lists.Count == 0)
                lists = await SeedDefaultBoardAsync();

            foreach (var list in lists)
                Lists.Add(CreateListViewModel(list));
        }
        catch (Exception ex)
        {
            ErrorMessage = $"데이터를 불러오지 못했어요: {ex.Message}";
        }
    }

    private async Task<IReadOnlyList<BoardList>> SeedDefaultBoardAsync()
    {
        var todo = await _repository.AddListAsync(_boardId, "To Do");
        await _repository.AddListAsync(_boardId, "In Progress");
        await _repository.AddListAsync(_boardId, "Done");
        await _repository.AddCardAsync(todo.Id, "카드를 드래그해서 옮겨 보세요", "✎로 제목을 수정하고 ✕로 삭제할 수 있어요");

        return await _repository.GetListsAsync(_boardId);
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
            var model = await _repository.AddListAsync(_boardId, name);
            Lists.Add(CreateListViewModel(model));
        }, "리스트를 추가하지 못했어요");
    }
}
