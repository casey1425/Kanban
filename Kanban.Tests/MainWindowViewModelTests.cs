using CommunityToolkit.Mvvm.Messaging;
using Kanban.App.Messages;
using Kanban.App.Services;
using Kanban.App.ViewModels;
using Kanban.Tests.Support;
using Microsoft.Data.Sqlite;

namespace Kanban.Tests;

public sealed class MainWindowViewModelTests : IDisposable
{
    private readonly TestApp _app = new();

    public void Dispose() => _app.Dispose();

    private static async Task AddCardAsync(BoardListViewModel list, string title)
    {
        list.NewCardTitle = title;
        await list.AddNewCardCommand.ExecuteAsync(null);
    }

    [Fact]
    public async Task First_run_creates_default_board()
    {
        var app = await _app.StartAsync();

        Assert.Null(app.ErrorMessage);
        Assert.Equal(new[] { "To Do", "In Progress", "Done" }, app.SelectedBoard!.Lists.Select(l => l.Name));
        Assert.Single(app.SelectedBoard!.Lists[0].Cards); // 안내용 카드 하나
    }

    [Fact]
    public async Task Board_is_restored_after_restart()
    {
        var app = await _app.StartAsync();
        await AddCardAsync(app.SelectedBoard!.Lists[0], "A");
        app.SelectedBoard!.NewListName = "Review";
        await app.SelectedBoard!.AddNewListCommand.ExecuteAsync(null);

        var restarted = await _app.StartAsync();

        Assert.Contains("Review", restarted.SelectedBoard!.Lists.Select(l => l.Name));
        Assert.Contains("A", restarted.SelectedBoard!.Lists[0].Cards.Select(c => c.Title));
    }

    [Fact]
    public async Task Add_button_is_disabled_for_blank_input()
    {
        var app = await _app.StartAsync();
        var list = app.SelectedBoard!.Lists[0];

        list.NewCardTitle = "   ";
        Assert.False(list.AddNewCardCommand.CanExecute(null));

        list.NewCardTitle = "ok";
        Assert.True(list.AddNewCardCommand.CanExecute(null));
    }

    [Fact]
    public async Task Moved_card_stays_moved_after_restart()
    {
        var app = await _app.StartAsync();
        var todo = app.SelectedBoard!.Lists[0];
        await AddCardAsync(todo, "A");
        await AddCardAsync(todo, "B");

        await app.SelectedBoard!.Lists[1].MoveCardHereAsync(todo.Cards.First(c => c.Title == "A"), 0);
        await todo.MoveCardHereAsync(todo.Cards.First(c => c.Title == "B"), 0);

        var restarted = await _app.StartAsync();
        Assert.Equal(new[] { "B", "카드를 드래그해서 옮겨 보세요" }, restarted.SelectedBoard!.Lists[0].Cards.Select(c => c.Title));
        Assert.Equal(new[] { "A" }, restarted.SelectedBoard!.Lists[1].Cards.Select(c => c.Title));
    }

    [Fact]
    public async Task Deleting_a_card_or_list_is_persisted()
    {
        var app = await _app.StartAsync();
        await AddCardAsync(app.SelectedBoard!.Lists[0], "A");
        await app.SelectedBoard!.Lists[0].Cards.First(c => c.Title == "A").DeleteCommand.ExecuteAsync(null);
        await app.SelectedBoard!.Lists[1].DeleteCommand.ExecuteAsync(null);

        var restarted = await _app.StartAsync();

        Assert.DoesNotContain("A", restarted.SelectedBoard!.Lists[0].Cards.Select(c => c.Title));
        Assert.DoesNotContain("In Progress", restarted.SelectedBoard!.Lists.Select(l => l.Name));
    }

    [Fact]
    public async Task Card_title_edit_is_saved_and_blank_edit_is_ignored()
    {
        var app = await _app.StartAsync();
        var card = app.SelectedBoard!.Lists[0].Cards[0];
        var original = card.Title;

        card.BeginEditCommand.Execute(null);
        card.EditTitle = "   ";
        await card.CommitEditCommand.ExecuteAsync(null);
        Assert.Equal(original, card.Title);
        Assert.False(card.IsEditing);

        card.BeginEditCommand.Execute(null);
        card.EditTitle = "  새 제목  ";
        await card.CommitEditCommand.ExecuteAsync(null);

        var restarted = await _app.StartAsync();
        Assert.Equal("새 제목", restarted.SelectedBoard!.Lists[0].Cards[0].Title);
    }

    [Fact]
    public async Task Save_failure_shows_error_instead_of_throwing()
    {
        var app = await _app.StartAsync();

        // DB 파일이 사라진 상황을 만든다: 연결 풀을 비우고 파일을 지우면 다음 접근은 실패한다.
        SqliteConnection.ClearAllPools();
        File.Delete(_app.DatabasePath);

        await AddCardAsync(app.SelectedBoard!.Lists[0], "will fail");

        Assert.False(string.IsNullOrEmpty(app.ErrorMessage));
        app.DismissErrorCommand.Execute(null);
        Assert.Null(app.ErrorMessage);
    }

    [Fact]
    public async Task Clicking_a_card_opens_detail_and_closing_clears_it()
    {
        var app = await _app.StartAsync();
        var card = app.SelectedBoard!.Lists[0].Cards[0];

        card.OpenDetailCommand.Execute(null);
        Assert.Same(card, app.Detail?.Card);

        await app.Detail!.CloseCommand.ExecuteAsync(null);
        Assert.Null(app.Detail);
    }

    [Fact]
    public void Theme_choice_is_persisted_across_restarts()
    {
        var app = _app.CreateViewModel();
        Assert.Equal(ThemeMode.System, app.ThemeMode);

        app.CycleThemeCommand.Execute(null);
        app.CycleThemeCommand.Execute(null);
        Assert.Equal(ThemeMode.Dark, app.ThemeMode);

        var restarted = _app.CreateViewModel();
        Assert.Equal(ThemeMode.Dark, restarted.ThemeMode);

        restarted.CycleThemeCommand.Execute(null); // Dark -> System
        Assert.Equal(ThemeMode.System, restarted.ThemeMode);
    }

    [Fact]
    public void Corrupt_settings_file_falls_back_to_defaults()
    {
        File.WriteAllText(_app.SettingsPath, "{ not json");

        var app = _app.CreateViewModel();

        Assert.Equal(ThemeMode.System, app.ThemeMode);
        Assert.Null(app.ErrorMessage);
    }

    [Fact]
    public void Error_messages_sent_through_the_messenger_reach_the_banner()
    {
        var app = _app.CreateViewModel();

        WeakReferenceMessenger.Default.Send(new ErrorOccurred("boom"));

        Assert.Equal("boom", app.ErrorMessage);
    }

    // ----- 다중 보드 -----

    private static async Task<BoardViewModel> AddBoardAsync(MainWindowViewModel app, string name)
    {
        app.NewBoardName = name;
        await app.AddNewBoardCommand.ExecuteAsync(null);
        return app.SelectedBoard!;
    }

    [Fact]
    public async Task First_run_has_one_selected_default_board()
    {
        var app = await _app.StartAsync();

        var board = Assert.Single(app.Boards);
        Assert.Same(board, app.SelectedBoard);
        Assert.Equal("My Kanban Board", board.Name);
        Assert.True(board.IsLoaded);
    }

    [Fact]
    public async Task New_board_is_selected_and_comes_with_default_lists()
    {
        var app = await _app.StartAsync();

        var board = await AddBoardAsync(app, "  Side project  ");

        Assert.Equal(2, app.Boards.Count);
        Assert.Equal("Side project", board.Name);
        Assert.Equal(new[] { "To Do", "In Progress", "Done" }, board.Lists.Select(l => l.Name));
        Assert.All(board.Lists, l => Assert.Empty(l.Cards)); // 안내 카드는 첫 실행 보드에만 있다.
    }

    [Fact]
    public async Task Board_name_must_not_be_blank()
    {
        var app = await _app.StartAsync();

        app.NewBoardName = "  ";
        Assert.False(app.AddNewBoardCommand.CanExecute(null));

        app.NewBoardName = "x";
        Assert.True(app.AddNewBoardCommand.CanExecute(null));
    }

    [Fact]
    public async Task Boards_are_restored_in_order_and_only_the_selected_one_is_loaded()
    {
        var app = await _app.StartAsync();
        var second = await AddBoardAsync(app, "Second");
        await AddCardAsync(second.Lists[0], "in second");
        app.SelectedBoard = app.Boards[0]; // 마지막으로 보던 보드를 첫 번째로 만든다.

        var restarted = await _app.StartAsync();

        Assert.Equal(new[] { "My Kanban Board", "Second" }, restarted.Boards.Select(b => b.Name));
        Assert.True(restarted.Boards[0].IsLoaded);   // 선택된 보드만 먼저 불러온다.
        Assert.False(restarted.Boards[1].IsLoaded);

        await restarted.Boards[1].LoadAsync();
        Assert.Equal("in second", restarted.Boards[1].Lists[0].Cards.Single().Title);
    }

    [Fact]
    public async Task Last_viewed_board_is_reopened_after_restart()
    {
        var app = await _app.StartAsync();
        await AddBoardAsync(app, "Second"); // 새 보드는 만들면서 선택된다.

        var restarted = await _app.StartAsync();

        Assert.Equal("Second", restarted.SelectedBoard!.Name);
        Assert.True(restarted.SelectedBoard.IsLoaded);
    }

    [Fact]
    public async Task Renaming_a_board_is_saved_and_blank_names_are_ignored()
    {
        var app = await _app.StartAsync();
        var board = app.SelectedBoard!;

        board.BeginRenameCommand.Execute(null);
        board.EditName = "   ";
        await board.CommitRenameCommand.ExecuteAsync(null);
        Assert.Equal("My Kanban Board", board.Name);
        Assert.False(board.IsRenaming);

        board.BeginRenameCommand.Execute(null);
        board.EditName = "  새 이름  ";
        await board.CommitRenameCommand.ExecuteAsync(null);
        Assert.Equal("새 이름", board.Name);

        var restarted = await _app.StartAsync();
        Assert.Equal("새 이름", restarted.Boards[0].Name);
    }

    [Fact]
    public async Task Rename_can_be_cancelled()
    {
        var app = await _app.StartAsync();
        var board = app.SelectedBoard!;

        board.BeginRenameCommand.Execute(null);
        board.EditName = "discard me";
        board.CancelRenameCommand.Execute(null);

        Assert.Equal("My Kanban Board", board.Name);
        Assert.False(board.IsRenaming);
    }

    [Fact]
    public async Task The_only_board_cannot_be_deleted()
    {
        var app = await _app.StartAsync();
        var board = app.SelectedBoard!;
        Assert.False(board.CanDelete);

        board.RequestDeleteCommand.Execute(null);

        Assert.False(board.IsConfirmingDelete);
        await board.ConfirmDeleteCommand.ExecuteAsync(null);
        Assert.Single(app.Boards);
    }

    [Fact]
    public async Task Deleting_a_board_needs_confirmation_and_selects_a_neighbour()
    {
        var app = await _app.StartAsync();
        var second = await AddBoardAsync(app, "Second");
        await AddCardAsync(second.Lists[0], "doomed");
        Assert.True(second.CanDelete);
        Assert.True(app.Boards[0].CanDelete);

        second.RequestDeleteCommand.Execute(null);
        Assert.True(second.IsConfirmingDelete);
        second.CancelDeleteCommand.Execute(null);
        Assert.Equal(2, app.Boards.Count); // 취소하면 그대로다.

        second.RequestDeleteCommand.Execute(null);
        await second.ConfirmDeleteCommand.ExecuteAsync(null);

        Assert.Single(app.Boards);
        Assert.Same(app.Boards[0], app.SelectedBoard);
        Assert.False(app.Boards[0].CanDelete); // 하나만 남으면 다시 삭제 불가

        var restarted = await _app.StartAsync();
        Assert.Equal(new[] { "My Kanban Board" }, restarted.Boards.Select(b => b.Name));
    }

    [Fact]
    public async Task Cards_and_filters_are_isolated_between_boards()
    {
        var app = await _app.StartAsync();
        var first = app.SelectedBoard!;
        var second = await AddBoardAsync(app, "Second");
        await AddCardAsync(second.Lists[0], "only in second");

        first.SearchText = "zzz";   // 첫 보드에서만 검색

        Assert.Equal("zzz", first.SearchText);
        Assert.Equal("", second.SearchText);
        Assert.True(second.Lists[0].Cards.All(c => c.IsMatch));
        Assert.DoesNotContain(first.Lists.SelectMany(l => l.Cards), c => c.Title == "only in second");
    }

    // ----- 카드를 다른 보드로 옮기기 -----

    [Fact]
    public async Task Card_moves_to_the_end_of_the_first_list_of_another_board_and_stays_after_restart()
    {
        var app = await _app.StartAsync();
        var first = app.SelectedBoard!;
        var second = await AddBoardAsync(app, "Second");
        await AddCardAsync(second.Lists[0], "already there");
        app.SelectedBoard = first;

        await AddCardAsync(first.Lists[0], "traveler");
        var card = first.Lists[0].Cards.Single(c => c.Title == "traveler");

        await app.MoveCardToBoardAsync(card, second);

        Assert.DoesNotContain(first.Lists.SelectMany(l => l.Cards), c => c.Title == "traveler");
        Assert.Equal(new[] { "already there", "traveler" }, second.Lists[0].Cards.Select(c => c.Title));
        Assert.Same(second.Lists[0], card.Owner);
        Assert.Null(app.ErrorMessage);
        Assert.Contains("Second", app.InfoMessage);

        var restarted = await _app.StartAsync();
        var restoredSecond = restarted.Boards.Single(b => b.Name == "Second");
        await restoredSecond.LoadAsync();
        Assert.Equal(new[] { "already there", "traveler" }, restoredSecond.Lists[0].Cards.Select(c => c.Title));
        Assert.DoesNotContain(restarted.Boards[0].Lists.SelectMany(l => l.Cards), c => c.Title == "traveler");
    }

    [Fact]
    public async Task Moving_to_a_board_that_is_not_loaded_yet_loads_it_first()
    {
        var app = await _app.StartAsync();
        await AddBoardAsync(app, "Second");

        var restarted = await _app.StartAsync();           // 마지막 보드(Second)가 열린 상태로 시작
        var other = restarted.Boards.Single(b => b.Name == "My Kanban Board");
        Assert.False(other.IsLoaded);

        await AddCardAsync(restarted.SelectedBoard!.Lists[0], "to move");
        var card = restarted.SelectedBoard.Lists[0].Cards.Single(c => c.Title == "to move");

        await restarted.MoveCardToBoardAsync(card, other);

        Assert.True(other.IsLoaded);
        Assert.Contains("to move", other.Lists[0].Cards.Select(c => c.Title));
        Assert.Null(restarted.ErrorMessage);
    }

    [Fact]
    public async Task Moving_to_the_same_board_does_nothing()
    {
        var app = await _app.StartAsync();
        var board = app.SelectedBoard!;
        var card = board.Lists[0].Cards[0];

        await app.MoveCardToBoardAsync(card, board);

        Assert.Same(board.Lists[0], card.Owner);
        Assert.Null(app.InfoMessage);
    }

    [Fact]
    public async Task Moving_to_a_board_without_lists_shows_an_error_and_keeps_the_card()
    {
        var app = await _app.StartAsync();
        var first = app.SelectedBoard!;
        var empty = await AddBoardAsync(app, "Empty");
        foreach (var list in empty.Lists.ToList())
            await list.DeleteCommand.ExecuteAsync(null);
        app.SelectedBoard = first;
        var card = first.Lists[0].Cards[0];

        await app.MoveCardToBoardAsync(card, empty);

        Assert.Contains("Empty", app.ErrorMessage);
        Assert.Same(first.Lists[0], card.Owner);
        Assert.Null(app.InfoMessage);
    }

    [Fact]
    public async Task Labels_checklist_and_due_date_survive_a_board_move()
    {
        var app = await _app.StartAsync();
        var first = app.SelectedBoard!;
        var second = await AddBoardAsync(app, "Second");
        app.SelectedBoard = first;

        var card = first.Lists[0].Cards[0];
        await card.LabelOptions[0].ToggleCommand.ExecuteAsync(null);
        card.NewChecklistText = "item";
        await card.AddChecklistItemCommand.ExecuteAsync(null);
        card.DueDate = new DateOnly(2031, 2, 3);
        await card.SaveAsync();

        await app.MoveCardToBoardAsync(card, second);

        var restarted = await _app.StartAsync();
        var restoredSecond = restarted.Boards.Single(b => b.Name == "Second");
        await restoredSecond.LoadAsync();
        var moved = restoredSecond.Lists[0].Cards.Single();
        Assert.Equal(new[] { 1 }, moved.LabelOptions.Where(o => o.IsSelected).Select(o => o.Id));
        Assert.Equal("item", moved.Checklist.Single().Text);
        Assert.Equal(new DateOnly(2031, 2, 3), moved.DueDate);
    }

    [Fact]
    public async Task Info_message_is_cleared_when_the_board_changes()
    {
        var app = await _app.StartAsync();
        var first = app.SelectedBoard!;
        var second = await AddBoardAsync(app, "Second");
        app.SelectedBoard = first;
        await app.MoveCardToBoardAsync(first.Lists[0].Cards[0], second);
        Assert.NotNull(app.InfoMessage);

        app.SelectedBoard = second;

        Assert.Null(app.InfoMessage);
    }
}
