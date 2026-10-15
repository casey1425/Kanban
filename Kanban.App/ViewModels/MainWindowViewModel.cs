using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Kanban.App.Messages;
using Kanban.App.Services;
using Kanban.Core;
using Kanban.Core.Models;

namespace Kanban.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private const string DefaultBoardName = "My Kanban Board";

    private readonly IBoardRepository _repository;
    private readonly ISettingsService _settings;
    private IReadOnlyList<Label> _labels = Array.Empty<Label>();

    // ----- 보드 목록 -----

    public ObservableCollection<BoardViewModel> Boards { get; } = new();

    /// <summary>지금 보고 있는 보드. 처음 선택될 때 리스트와 카드를 불러온다.</summary>
    [ObservableProperty]
    private BoardViewModel? _selectedBoard;

    partial void OnSelectedBoardChanged(BoardViewModel? value)
    {
        if (value is { IsLoaded: false })
            _ = RunSafeAsync(value.LoadAsync, "보드를 불러오지 못했어요");
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddNewBoardCommand))]
    private string _newBoardName = string.Empty;

    private bool CanAddNewBoard() => !string.IsNullOrWhiteSpace(NewBoardName);

    /// <summary>새 보드를 만들고(기본 리스트 3개 포함) 바로 그 보드로 이동한다.</summary>
    [RelayCommand(CanExecute = nameof(CanAddNewBoard))]
    private Task AddNewBoardAsync()
    {
        var name = NewBoardName.Trim();
        NewBoardName = string.Empty;

        return RunSafeAsync(async () =>
        {
            var model = await _repository.AddBoardAsync(name);
            await CreateDefaultListsAsync(model.Id);

            var board = new BoardViewModel(model, _repository, _labels);
            await board.LoadAsync();
            Boards.Add(board);
            SelectedBoard = board;
        }, "보드를 만들지 못했어요");
    }

    private async Task<BoardList> CreateDefaultListsAsync(int boardId)
    {
        var todo = await _repository.AddListAsync(boardId, "To Do");
        await _repository.AddListAsync(boardId, "In Progress");
        await _repository.AddListAsync(boardId, "Done");
        return todo;
    }

    // ----- 오류 / 상세 패널 -----

    /// <summary>불러오기/저장 실패 시 화면 상단에 보여 줄 메시지. 사용자가 닫을 수 있다.</summary>
    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>열려 있는 카드 상세 패널. null이면 닫혀 있다.</summary>
    [ObservableProperty]
    private CardDetailViewModel? _detail;

    [RelayCommand]
    private void DismissError() => ErrorMessage = null;

    [RelayCommand]
    private void CloseDetail() => Detail?.CloseCommand.Execute(null);

    // ----- 테마 -----

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

    // ----- 시작 -----

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

    /// <summary>DB를 준비하고 보드 목록을 불러온다. 처음 실행이면 기본 보드에 안내 카드를 만든다.</summary>
    public async Task InitializeAsync()
    {
        try
        {
            await _repository.InitializeAsync();
            _labels = await _repository.GetLabelsAsync();

            var boards = await _repository.GetBoardsAsync();
            if (boards.Count == 0)
                boards = new[] { await _repository.AddBoardAsync(DefaultBoardName) };

            foreach (var board in boards)
                Boards.Add(new BoardViewModel(board, _repository, _labels));

            if (Boards.Count == 1)
                await SeedFirstRunBoardAsync(Boards[0]);

            // 선택하기 전에 먼저 불러 둔다. (선택하면 불러오기가 백그라운드로 시작되므로)
            await Boards[0].LoadAsync();
            SelectedBoard = Boards[0];
        }
        catch (Exception ex)
        {
            ErrorMessage = $"데이터를 불러오지 못했어요: {ex.Message}";
        }
    }

    /// <summary>비어 있는 유일한 보드라면 기본 리스트와 안내 카드를 만든다.</summary>
    private async Task SeedFirstRunBoardAsync(BoardViewModel board)
    {
        var lists = await _repository.GetListsAsync(board.Id);
        if (lists.Count > 0)
            return;

        var todo = await CreateDefaultListsAsync(board.Id);
        await _repository.AddCardAsync(todo.Id, "카드를 드래그해서 옮겨 보세요", "✎로 제목을 수정하고 ✕로 삭제할 수 있어요");
    }
}
