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
    public async Task Boards_are_restored_in_order_and_loaded_lazily()
    {
        var app = await _app.StartAsync();
        var second = await AddBoardAsync(app, "Second");
        await AddCardAsync(second.Lists[0], "in second");

        var restarted = await _app.StartAsync();

        Assert.Equal(new[] { "My Kanban Board", "Second" }, restarted.Boards.Select(b => b.Name));
        Assert.True(restarted.Boards[0].IsLoaded);   // 선택된 보드만 먼저 불러온다.
        Assert.False(restarted.Boards[1].IsLoaded);

        await restarted.Boards[1].LoadAsync();
        Assert.Equal("in second", restarted.Boards[1].Lists[0].Cards.Single().Title);
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
}
