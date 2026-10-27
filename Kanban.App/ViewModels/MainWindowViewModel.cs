using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Kanban.App.Messages;
using Kanban.App.Services;
using Kanban.App.Undo;
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
        InfoMessage = null; // 보드를 바꾸면 이전 안내는 지운다.

        if (value is null)
            return;

        if (!value.IsLoaded)
            _ = RunSafeAsync(value.LoadAsync, "보드를 불러오지 못했어요");

        // 다음 실행 때 이 보드부터 열기 위해 기억해 둔다.
        _settings.Current.LastBoardId = value.Id;
        SaveSettings();
    }

    private void SaveSettings()
    {
        try
        {
            _settings.Save();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"설정을 저장하지 못했어요: {ex.Message}";
        }
    }

    private BoardViewModel CreateBoardViewModel(Board model) =>
        new(model, _repository, _labels, OnBoardDeleted);

    private void OnBoardDeleted(BoardViewModel deleted)
    {
        var index = Boards.IndexOf(deleted);
        Boards.Remove(deleted);

        // 지워진 보드의 리스트와 카드를 가리키는 기록은 더 이상 유효하지 않다.
        History.Clear();

        // 지운 보드를 보고 있었다면, 바로 앞(없으면 첫 번째) 보드로 이동한다.
        if (SelectedBoard == deleted || SelectedBoard is null)
            SelectedBoard = Boards[Math.Clamp(index - 1, 0, Boards.Count - 1)];
    }

    // 보드가 하나뿐이면 삭제 버튼을 숨긴다.
    private void UpdateCanDelete()
    {
        foreach (var board in Boards)
            board.CanDelete = Boards.Count > 1;
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddNewBoardCommand))]
    private string _newBoardName = string.Empty;

    /// <summary>
    /// 카드를 다른 보드의 첫 리스트 맨 끝으로 옮긴다. 같은 보드이면 아무것도 하지 않는다.
    /// 대상 보드를 아직 불러오지 않았다면 먼저 불러온다.
    /// </summary>
    public Task MoveCardToBoardAsync(CardViewModel card, BoardViewModel target) => RunSafeAsync(async () =>
    {
        if (card.Owner is null || target.Lists.Contains(card.Owner))
            return;

        await target.LoadAsync();

        var list = target.Lists.FirstOrDefault();
        if (list is null)
        {
            ErrorMessage = $"\"{target.Name}\" 보드에 리스트가 없어서 카드를 옮길 수 없어요";
            return;
        }

        var title = card.Title;
        if (await list.TryMoveCardHereAsync(card, list.Cards.Count))
            InfoMessage = $"\"{title}\" 카드를 \"{target.Name}\" 보드로 옮겼어요";
    }, "카드를 다른 보드로 옮기지 못했어요");

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

            var board = CreateBoardViewModel(model);
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

    // ----- 실행 취소 / 다시 실행 -----

    public UndoHistory History { get; } = new();

    // 되돌리는 동안 일어나는 변경이 새 기록으로 남지 않게 막는다.
    private bool _isReplaying;

    public string UndoToolTip => History.NextUndo is { } action
        ? $"실행 취소: {action.Description} (Ctrl+Z)"
        : "실행 취소할 작업이 없어요";

    public string RedoToolTip => History.NextRedo is { } action
        ? $"다시 실행: {action.Description} (Ctrl+Shift+Z)"
        : "다시 실행할 작업이 없어요";

    // 상세 패널이 열려 있는 동안에는 대부분 되돌리지 않는다. 편집 중인 카드가 사라질 수 있기 때문이다.
    // 예외: 열려 있는 바로 그 카드의 라벨/체크리스트 변경은 패널 안에서 일어난 일이므로 되돌릴 수 있다.
    private bool CanReplay(IUndoableAction? action) =>
        action is not null && (Detail is null || ReferenceEquals(action.DetailScope, Detail.Card));

    private bool CanUndo() => CanReplay(History.NextUndo);

    private bool CanRedo() => CanReplay(History.NextRedo);

    private void OnHistoryChanged()
    {
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(UndoToolTip));
        OnPropertyChanged(nameof(RedoToolTip));
    }

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private Task UndoAsync() => ReplayAsync(undo: true);

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private Task RedoAsync() => ReplayAsync(undo: false);

    private async Task ReplayAsync(bool undo)
    {
        var action = undo ? History.NextUndo : History.NextRedo;
        if (action is null)
            return;

        _isReplaying = true;
        try
        {
            // 작업이 일어난 보드를 보여 준 뒤 되돌린다. 안 그러면 화면에서 아무 일도 안 일어난 것처럼 보인다.
            var boardId = undo ? action.UndoBoardId : action.RedoBoardId;
            var board = boardId is { } id ? Boards.FirstOrDefault(b => b.Id == id) : null;
            if (board is not null && board != SelectedBoard)
            {
                await board.LoadAsync();
                SelectedBoard = board;
            }

            var ok = await TryRunAsync(
                undo ? action.UndoAsync : action.RedoAsync,
                undo ? "실행 취소하지 못했어요" : "다시 실행하지 못했어요");

            // 실패하면 기록에 그대로 남겨 둔다. 오류 배너가 이미 떠 있다.
            if (!ok)
                return;

            if (undo)
                History.CompleteUndo();
            else
                History.CompleteRedo();

            InfoMessage = $"{(undo ? "실행 취소" : "다시 실행")}: {action.Description}";
        }
        finally
        {
            _isReplaying = false;
        }
    }

    // ----- 오류 / 상세 패널 -----

    /// <summary>불러오기/저장 실패 시 화면 상단에 보여 줄 메시지. 사용자가 닫을 수 있다.</summary>
    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>열려 있는 카드 상세 패널. null이면 닫혀 있다.</summary>
    [ObservableProperty]
    private CardDetailViewModel? _detail;

    partial void OnDetailChanged(CardDetailViewModel? value) => OnHistoryChanged();

    /// <summary>작업 결과 안내(오류가 아닌 알림). 사용자가 닫거나 보드를 바꾸면 사라진다.</summary>
    [ObservableProperty]
    private string? _infoMessage;

    [RelayCommand]
    private void DismissInfo() => InfoMessage = null;

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
        SaveSettings();
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
            this, (vm, message) => vm.Detail = new CardDetailViewModel(
                message.Card, () => vm.Detail = null, vm.UndoCommand, vm.RedoCommand));

        WeakReferenceMessenger.Default.Register<MainWindowViewModel, ErrorOccurred>(
            this, (vm, message) => vm.ErrorMessage = message.Message);

        WeakReferenceMessenger.Default.Register<MainWindowViewModel, ActionPerformed>(
            this, (vm, message) =>
            {
                if (!vm._isReplaying)
                    vm.History.Push(message.Action);
            });

        History.Changed += (_, _) => OnHistoryChanged();
        Boards.CollectionChanged += (_, _) => UpdateCanDelete();
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
                Boards.Add(CreateBoardViewModel(board));

            if (Boards.Count == 1)
                await SeedFirstRunBoardAsync(Boards[0]);

            // 마지막으로 보던 보드가 있으면 그것을, 없으면(또는 삭제됐으면) 첫 번째를 연다.
            var initial = Boards.FirstOrDefault(b => b.Id == _settings.Current.LastBoardId) ?? Boards[0];

            // 선택하기 전에 먼저 불러 둔다. (선택하면 불러오기가 백그라운드로 시작되므로)
            await initial.LoadAsync();
            SelectedBoard = initial;
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
