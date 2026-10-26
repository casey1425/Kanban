using Kanban.App.ViewModels;
using Kanban.Tests.Support;
using Microsoft.Data.Sqlite;

namespace Kanban.Tests;

public sealed class UndoRedoTests : IDisposable
{
    private readonly TestApp _app = new();

    public void Dispose() => _app.Dispose();

    // ----- helpers -----

    /// <summary>빈 리스트 3개짜리 새 보드를 만들어 선택한다. 설정 과정의 기록은 비워서, 테스트가 시작할 때 기록이 깨끗하다.</summary>
    private static async Task<BoardViewModel> NewBoardAsync(MainWindowViewModel app, string name = "T")
    {
        app.NewBoardName = name;
        await app.AddNewBoardCommand.ExecuteAsync(null);
        app.History.Clear();
        return app.SelectedBoard!;
    }

    private static async Task<CardViewModel> AddCardAsync(BoardListViewModel list, string title)
    {
        list.NewCardTitle = title;
        await list.AddNewCardCommand.ExecuteAsync(null);
        return list.Cards.Single(c => c.Title == title);
    }

    private static string Titles(BoardListViewModel list) => string.Join(",", list.Cards.Select(c => c.Title));

    private async Task<BoardViewModel> ReloadBoardAsync(string name)
    {
        var restarted = await _app.StartAsync();
        var board = restarted.Boards.Single(b => b.Name == name);
        await board.LoadAsync();
        return board;
    }

    private static Task UndoAsync(MainWindowViewModel app) => app.UndoCommand.ExecuteAsync(null);

    private static Task RedoAsync(MainWindowViewModel app) => app.RedoCommand.ExecuteAsync(null);

    // ----- 카드 이동 -----

    [Fact]
    public async Task Moving_a_card_within_a_list_can_be_undone_and_redone_and_is_persisted()
    {
        var app = await _app.StartAsync();
        var board = await NewBoardAsync(app);
        var list = board.Lists[0];
        var a = await AddCardAsync(list, "A");
        await AddCardAsync(list, "B");
        await AddCardAsync(list, "C");
        app.History.Clear();

        await list.MoveCardHereAsync(a, list.Cards.Count);   // A를 맨 뒤로
        Assert.Equal("B,C,A", Titles(list));

        await UndoAsync(app);
        Assert.Equal("A,B,C", Titles(list));
        Assert.Equal("A,B,C", Titles((await ReloadBoardAsync("T")).Lists[0]));

        await RedoAsync(app);
        Assert.Equal("B,C,A", Titles(list));
        Assert.Equal("B,C,A", Titles((await ReloadBoardAsync("T")).Lists[0]));
    }

    [Fact]
    public async Task Moving_a_card_to_another_list_can_be_undone_to_its_original_position()
    {
        var app = await _app.StartAsync();
        var board = await NewBoardAsync(app);
        var from = board.Lists[0];
        var to = board.Lists[1];
        await AddCardAsync(from, "A");
        var b = await AddCardAsync(from, "B");
        await AddCardAsync(from, "C");
        await AddCardAsync(to, "X");
        app.History.Clear();

        await to.MoveCardHereAsync(b, 0);
        Assert.Equal("A,C", Titles(from));
        Assert.Equal("B,X", Titles(to));

        await UndoAsync(app);
        Assert.Equal("A,B,C", Titles(from));
        Assert.Equal("X", Titles(to));
        Assert.Same(from, b.Owner);

        var reloaded = await ReloadBoardAsync("T");
        Assert.Equal("A,B,C", Titles(reloaded.Lists[0]));
        Assert.Equal("X", Titles(reloaded.Lists[1]));

        await RedoAsync(app);
        Assert.Equal("A,C", Titles(from));
        Assert.Equal("B,X", Titles(to));
    }

    [Fact]
    public async Task Putting_a_card_back_where_it_was_is_not_recorded()
    {
        var app = await _app.StartAsync();
        var board = await NewBoardAsync(app);
        var list = board.Lists[0];
        var a = await AddCardAsync(list, "A");
        await AddCardAsync(list, "B");
        app.History.Clear();

        await list.MoveCardHereAsync(a, 0);   // 이미 맨 앞
        await list.MoveCardHereAsync(a, 1);   // 자기 바로 뒤 = 같은 자리

        Assert.False(app.History.CanUndo);
        Assert.Equal("A,B", Titles(list));
    }

    [Fact]
    public async Task Moving_a_card_to_another_board_can_be_undone_and_switches_back_to_the_source_board()
    {
        var app = await _app.StartAsync();
        var source = await NewBoardAsync(app, "Source");
        var card = await AddCardAsync(source.Lists[0], "traveler");
        var target = await NewBoardAsync(app, "Target");
        app.SelectedBoard = source;
        app.History.Clear();

        await app.MoveCardToBoardAsync(card, target);
        Assert.Equal("traveler", Titles(target.Lists[0]));

        app.SelectedBoard = target;       // 사용자가 다른 보드를 보고 있는 상황
        await UndoAsync(app);

        Assert.Same(source, app.SelectedBoard);               // 되돌린 작업이 일어난 보드로 이동했다
        Assert.Equal("traveler", Titles(source.Lists[0]));
        Assert.Empty(target.Lists[0].Cards);

        var reloadedSource = await ReloadBoardAsync("Source");
        Assert.Equal("traveler", Titles(reloadedSource.Lists[0]));

        await RedoAsync(app);
        Assert.Same(target, app.SelectedBoard);
        Assert.Equal("traveler", Titles(target.Lists[0]));
    }

    // ----- 카드 추가 / 삭제 -----

    [Fact]
    public async Task Deleting_a_card_can_be_undone_with_everything_restored()
    {
        var app = await _app.StartAsync();
        var board = await NewBoardAsync(app);
        var list = board.Lists[0];
        await AddCardAsync(list, "A");
        var b = await AddCardAsync(list, "B");
        await AddCardAsync(list, "C");

        await b.LabelOptions[0].ToggleCommand.ExecuteAsync(null);
        b.NewChecklistText = "item";
        await b.AddChecklistItemCommand.ExecuteAsync(null);
        b.Checklist[0].IsDone = true;
        await b.Checklist[0].ToggleCommand.ExecuteAsync(null);
        b.Description = "desc";
        b.DueDate = new DateOnly(2031, 2, 3);
        await b.SaveAsync();
        var idBefore = b.Id;
        app.History.Clear();

        await b.DeleteCommand.ExecuteAsync(null);
        Assert.Equal("A,C", Titles(list));

        await UndoAsync(app);

        Assert.Equal("A,B,C", Titles(list));
        Assert.Same(b, list.Cards[1]);              // 같은 인스턴스, 같은 자리
        Assert.Same(list, b.Owner);
        Assert.Equal(idBefore, b.Id);

        var restored = (await ReloadBoardAsync("T")).Lists[0].Cards[1];
        Assert.Equal(idBefore, restored.Id);
        Assert.Equal("desc", restored.Description);
        Assert.Equal(new DateOnly(2031, 2, 3), restored.DueDate);
        Assert.Equal(new[] { 1 }, restored.LabelOptions.Where(o => o.IsSelected).Select(o => o.Id));
        var item = Assert.Single(restored.Checklist);
        Assert.Equal("item", item.Text);
        Assert.True(item.IsDone);

        await RedoAsync(app);
        Assert.Equal("A,C", Titles(list));
        Assert.Equal("A,C", Titles((await ReloadBoardAsync("T")).Lists[0]));
    }

    [Fact]
    public async Task Adding_a_card_can_be_undone_and_redone()
    {
        var app = await _app.StartAsync();
        var board = await NewBoardAsync(app);
        var list = board.Lists[0];
        await AddCardAsync(list, "A");
        app.History.Clear();

        var b = await AddCardAsync(list, "B");
        Assert.Equal("A,B", Titles(list));

        await UndoAsync(app);
        Assert.Equal("A", Titles(list));
        Assert.Equal("A", Titles((await ReloadBoardAsync("T")).Lists[0]));

        await RedoAsync(app);
        Assert.Equal("A,B", Titles(list));
        Assert.Same(b, list.Cards[1]);
        Assert.Equal("A,B", Titles((await ReloadBoardAsync("T")).Lists[0]));
    }

    [Fact]
    public async Task A_card_restored_by_undo_matches_the_active_filter()
    {
        var app = await _app.StartAsync();
        var board = await NewBoardAsync(app);
        var list = board.Lists[0];
        var apple = await AddCardAsync(list, "Apple");
        await AddCardAsync(list, "Banana");
        app.History.Clear();

        await apple.DeleteCommand.ExecuteAsync(null);
        board.SearchText = "banana";     // 지운 동안 필터를 켠다
        await UndoAsync(app);

        Assert.False(apple.IsMatch);     // 복원돼도 필터 조건을 따른다
        Assert.True(list.Cards.Single(c => c.Title == "Banana").IsMatch);
    }

    // ----- 카드 수정 -----

    [Fact]
    public async Task Inline_title_edit_can_be_undone_and_redone()
    {
        var app = await _app.StartAsync();
        var board = await NewBoardAsync(app);
        var card = await AddCardAsync(board.Lists[0], "Old");
        app.History.Clear();

        card.BeginEditCommand.Execute(null);
        card.EditTitle = "New";
        await card.CommitEditCommand.ExecuteAsync(null);
        Assert.Equal("New", card.Title);

        await UndoAsync(app);
        Assert.Equal("Old", card.Title);
        Assert.Equal("Old", (await ReloadBoardAsync("T")).Lists[0].Cards[0].Title);

        await RedoAsync(app);
        Assert.Equal("New", card.Title);
        Assert.Equal("New", (await ReloadBoardAsync("T")).Lists[0].Cards[0].Title);
    }

    [Fact]
    public async Task Editing_in_the_detail_panel_is_one_undoable_step()
    {
        var app = await _app.StartAsync();
        var board = await NewBoardAsync(app);
        var card = await AddCardAsync(board.Lists[0], "Title");
        app.History.Clear();

        card.OpenDetailCommand.Execute(null);
        card.Title = "Changed";
        card.Description = "details";
        card.DueDateTime = new DateTime(2032, 6, 7);
        await app.Detail!.CloseCommand.ExecuteAsync(null);

        await UndoAsync(app);   // 제목, 설명, 마감일이 한 번에 돌아온다
        Assert.Equal("Title", card.Title);
        Assert.Null(card.Description);
        Assert.Null(card.DueDate);
        Assert.False(app.History.CanUndo);

        var reloaded = (await ReloadBoardAsync("T")).Lists[0].Cards[0];
        Assert.Equal("Title", reloaded.Title);
        Assert.Null(reloaded.Description);
        Assert.Null(reloaded.DueDate);

        await RedoAsync(app);
        Assert.Equal("Changed", card.Title);
        Assert.Equal("details", card.Description);
        Assert.Equal(new DateOnly(2032, 6, 7), card.DueDate);
    }

    [Fact]
    public async Task Saving_without_changes_is_not_recorded()
    {
        var app = await _app.StartAsync();
        var board = await NewBoardAsync(app);
        var card = await AddCardAsync(board.Lists[0], "Same");
        app.History.Clear();

        card.OpenDetailCommand.Execute(null);
        card.Description = "   ";   // 공백은 "설명 없음"과 같으므로 변경이 아니다
        await app.Detail!.CloseCommand.ExecuteAsync(null);

        Assert.False(app.History.CanUndo);
    }

    // ----- 기록 규칙 -----

    [Fact]
    public async Task Several_actions_are_undone_in_reverse_order()
    {
        var app = await _app.StartAsync();
        var board = await NewBoardAsync(app);
        var list = board.Lists[0];

        var a = await AddCardAsync(list, "A");                    // 1) 추가
        await AddCardAsync(list, "B");                            // 2) 추가
        await list.MoveCardHereAsync(a, 2);                       // 3) A를 뒤로
        a.BeginEditCommand.Execute(null);
        a.EditTitle = "A2";
        await a.CommitEditCommand.ExecuteAsync(null);             // 4) 제목 수정
        Assert.Equal("B,A2", Titles(list));

        await UndoAsync(app);
        Assert.Equal("B,A", Titles(list));
        await UndoAsync(app);
        Assert.Equal("A,B", Titles(list));
        await UndoAsync(app);
        Assert.Equal("A", Titles(list));
        await UndoAsync(app);
        Assert.Empty(list.Cards);
        Assert.False(app.History.CanUndo);
        Assert.True(app.History.CanRedo);

        await RedoAsync(app);
        await RedoAsync(app);
        await RedoAsync(app);
        await RedoAsync(app);
        Assert.Equal("B,A2", Titles(list));
        Assert.Equal("B,A2", Titles((await ReloadBoardAsync("T")).Lists[0]));
    }

    [Fact]
    public async Task A_new_action_discards_the_redo_history()
    {
        var app = await _app.StartAsync();
        var board = await NewBoardAsync(app);
        var list = board.Lists[0];
        await AddCardAsync(list, "A");
        await UndoAsync(app);
        Assert.True(app.History.CanRedo);

        await AddCardAsync(list, "B");

        Assert.False(app.History.CanRedo);
    }

    [Fact]
    public async Task Undo_and_redo_are_disabled_while_the_detail_panel_is_open()
    {
        var app = await _app.StartAsync();
        var board = await NewBoardAsync(app);
        var card = await AddCardAsync(board.Lists[0], "A");
        Assert.True(app.UndoCommand.CanExecute(null));

        card.OpenDetailCommand.Execute(null);
        Assert.False(app.UndoCommand.CanExecute(null));

        await app.Detail!.CloseCommand.ExecuteAsync(null);
        Assert.True(app.UndoCommand.CanExecute(null));
    }

    [Fact]
    public async Task Deleting_a_board_clears_the_history()
    {
        var app = await _app.StartAsync();
        await NewBoardAsync(app, "One");
        var two = await NewBoardAsync(app, "Two");
        await AddCardAsync(two.Lists[0], "A");
        Assert.True(app.History.CanUndo);

        two.RequestDeleteCommand.Execute(null);
        await two.ConfirmDeleteCommand.ExecuteAsync(null);

        Assert.False(app.History.CanUndo);
        Assert.False(app.History.CanRedo);
    }

    [Fact]
    public async Task Undo_reports_what_it_undid()
    {
        var app = await _app.StartAsync();
        var board = await NewBoardAsync(app);
        var card = await AddCardAsync(board.Lists[0], "A");
        app.History.Clear();
        await card.DeleteCommand.ExecuteAsync(null);

        Assert.Contains("카드 삭제", app.UndoToolTip);

        await UndoAsync(app);
        Assert.Contains("실행 취소", app.InfoMessage);
        Assert.Contains("카드 삭제", app.InfoMessage);
        Assert.Contains("카드 삭제", app.RedoToolTip);

        await RedoAsync(app);
        Assert.Contains("다시 실행", app.InfoMessage);
    }

    [Fact]
    public async Task A_failing_undo_shows_an_error_and_keeps_the_action_in_the_history()
    {
        var app = await _app.StartAsync();
        var board = await NewBoardAsync(app);
        var card = await AddCardAsync(board.Lists[0], "A");
        app.History.Clear();
        await card.DeleteCommand.ExecuteAsync(null);

        // DB 파일이 사라진 상황: 복원 저장이 실패한다.
        SqliteConnection.ClearAllPools();
        File.Delete(_app.DatabasePath);

        await UndoAsync(app);   // 예외가 밖으로 나오면 여기서 테스트가 실패한다

        Assert.False(string.IsNullOrEmpty(app.ErrorMessage));
        Assert.True(app.History.CanUndo);
        Assert.False(app.History.CanRedo);
    }
}
