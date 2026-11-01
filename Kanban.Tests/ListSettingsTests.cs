using Kanban.App.ViewModels;
using Kanban.Tests.Support;

namespace Kanban.Tests;

public sealed class ListSettingsTests : IDisposable
{
    private readonly TestApp _app = new();

    public void Dispose() => _app.Dispose();

    // ----- helpers -----

    private static async Task<BoardViewModel> NewBoardAsync(MainWindowViewModel app, string name = "T")
    {
        app.NewBoardName = name;
        await app.AddNewBoardCommand.ExecuteAsync(null);
        app.History.Clear();
        return app.SelectedBoard!;
    }

    private static async Task AddCardAsync(BoardListViewModel list, string title)
    {
        list.NewCardTitle = title;
        await list.AddNewCardCommand.ExecuteAsync(null);
    }

    private async Task<BoardViewModel> ReloadBoardAsync(string name)
    {
        var restarted = await _app.StartAsync();
        var board = restarted.Boards.Single(b => b.Name == name);
        await board.LoadAsync();
        return board;
    }

    /// <summary>설정 패널을 열고 값을 채운 뒤 저장한다(사용자가 하는 순서 그대로).</summary>
    private static async Task SaveSettingsAsync(BoardListViewModel list, string? name = null, bool? isDone = null, decimal? wip = null, bool setWip = false)
    {
        list.ToggleSettingsCommand.Execute(null);
        if (name is not null) list.EditName = name;
        if (isDone is { } d) list.EditIsDoneList = d;
        if (setWip) list.EditWipLimit = wip;
        await list.SaveSettingsCommand.ExecuteAsync(null);
    }

    // ----- 기본값 -----

    [Fact]
    public async Task New_boards_default_lists_mark_only_Done_as_a_done_list()
    {
        var app = await _app.StartAsync();
        var board = await NewBoardAsync(app);

        Assert.Equal(new[] { false, false, true }, board.Lists.Select(l => l.IsDoneList));
        Assert.All(board.Lists, l => Assert.Null(l.WipLimit));

        var reloaded = await ReloadBoardAsync("T");
        Assert.Equal(new[] { false, false, true }, reloaded.Lists.Select(l => l.IsDoneList));
    }

    [Fact]
    public async Task A_list_added_by_the_user_starts_without_settings()
    {
        var app = await _app.StartAsync();
        var board = await NewBoardAsync(app);

        board.NewListName = "Review";
        await board.AddNewListCommand.ExecuteAsync(null);

        var review = board.Lists.Last();
        Assert.False(review.IsDoneList);
        Assert.Null(review.WipLimit);
        Assert.Equal("0", review.CardCountText);
    }

    // ----- 편집과 저장 -----

    [Fact]
    public async Task Settings_are_saved_and_restored_after_restart()
    {
        var app = await _app.StartAsync();
        var board = await NewBoardAsync(app);
        var list = board.Lists[1];

        await SaveSettingsAsync(list, name: "  Doing  ", isDone: true, wip: 4, setWip: true);

        Assert.False(list.IsSettingsOpen);
        Assert.Equal("Doing", list.Name);
        Assert.True(list.IsDoneList);
        Assert.Equal(4, list.WipLimit);

        var restored = (await ReloadBoardAsync("T")).Lists[1];
        Assert.Equal("Doing", restored.Name);
        Assert.True(restored.IsDoneList);
        Assert.Equal(4, restored.WipLimit);
    }

    [Fact]
    public async Task The_panel_starts_from_the_current_values_and_cancel_discards_edits()
    {
        var app = await _app.StartAsync();
        var board = await NewBoardAsync(app);
        var list = board.Lists[0];
        await SaveSettingsAsync(list, wip: 2, setWip: true);
        app.History.Clear();

        list.ToggleSettingsCommand.Execute(null);
        Assert.True(list.IsSettingsOpen);
        Assert.Equal("To Do", list.EditName);
        Assert.Equal(2, list.EditWipLimit);

        list.EditName = "discard me";
        list.EditWipLimit = 9;
        list.CancelSettingsCommand.Execute(null);

        Assert.False(list.IsSettingsOpen);
        Assert.Equal("To Do", list.Name);
        Assert.Equal(2, list.WipLimit);
        Assert.False(app.History.CanUndo);
    }

    [Theory]
    [InlineData(null, null)]      // 비어 있음 = 제한 없음
    [InlineData(0, null)]         // 0 이하 = 제한 없음
    [InlineData(-3, null)]
    [InlineData(1, 1)]
    [InlineData(7, 7)]
    [InlineData(5000, 999)]       // 터무니없이 큰 값은 상한으로 맞춘다
    public async Task WIP_limit_input_is_normalized(int? input, int? expected)
    {
        var app = await _app.StartAsync();
        var board = await NewBoardAsync(app);
        var list = board.Lists[0];
        await SaveSettingsAsync(list, wip: 3, setWip: true);   // 먼저 3으로 설정

        await SaveSettingsAsync(list, wip: input, setWip: true);

        Assert.Equal(expected, list.WipLimit);
    }

    [Fact]
    public async Task A_blank_name_keeps_the_old_name()
    {
        var app = await _app.StartAsync();
        var board = await NewBoardAsync(app);
        var list = board.Lists[0];

        await SaveSettingsAsync(list, name: "   ", isDone: true);

        Assert.Equal("To Do", list.Name);
        Assert.True(list.IsDoneList);   // 나머지 설정은 저장된다
    }

    [Fact]
    public async Task Clear_wip_limit_empties_the_field()
    {
        var app = await _app.StartAsync();
        var board = await NewBoardAsync(app);
        var list = board.Lists[0];
        await SaveSettingsAsync(list, wip: 5, setWip: true);

        list.ToggleSettingsCommand.Execute(null);
        list.ClearWipLimitCommand.Execute(null);
        await list.SaveSettingsCommand.ExecuteAsync(null);

        Assert.Null(list.WipLimit);
    }

    // ----- WIP 제한 표시 -----

    [Fact]
    public async Task Card_count_text_and_over_limit_follow_the_cards()
    {
        var app = await _app.StartAsync();
        var board = await NewBoardAsync(app);
        var list = board.Lists[0];
        Assert.Equal("0", list.CardCountText);
        Assert.False(list.IsOverWipLimit);

        await SaveSettingsAsync(list, wip: 2, setWip: true);
        Assert.Equal("0 / 2", list.CardCountText);

        await AddCardAsync(list, "A");
        await AddCardAsync(list, "B");
        Assert.Equal("2 / 2", list.CardCountText);
        Assert.False(list.IsOverWipLimit);          // 제한과 같으면 초과가 아니다

        await AddCardAsync(list, "C");
        Assert.Equal("3 / 2", list.CardCountText);
        Assert.True(list.IsOverWipLimit);

        await list.Cards[0].DeleteCommand.ExecuteAsync(null);
        Assert.False(list.IsOverWipLimit);
    }

    [Fact]
    public async Task Moving_cards_in_or_out_updates_both_lists_counts()
    {
        var app = await _app.StartAsync();
        var board = await NewBoardAsync(app);
        var from = board.Lists[0];
        var to = board.Lists[1];
        await SaveSettingsAsync(to, wip: 1, setWip: true);
        await AddCardAsync(from, "A");
        await AddCardAsync(to, "X");
        Assert.False(to.IsOverWipLimit);

        await to.MoveCardHereAsync(from.Cards[0], 0);

        Assert.Empty(from.Cards);
        Assert.Equal("2 / 1", to.CardCountText);
        Assert.True(to.IsOverWipLimit);          // 이동으로 제한을 넘어도 막지 않고 경고만 한다
    }

    [Fact]
    public async Task Cards_hidden_by_a_filter_still_count_toward_the_limit()
    {
        var app = await _app.StartAsync();
        var board = await NewBoardAsync(app);
        var list = board.Lists[0];
        await SaveSettingsAsync(list, wip: 1, setWip: true);
        await AddCardAsync(list, "Apple");
        await AddCardAsync(list, "Banana");

        board.SearchText = "apple";

        Assert.Equal(1, list.Cards.Count(c => c.IsMatch));
        Assert.True(list.IsOverWipLimit);
    }

    // ----- 실행 취소 -----

    [Fact]
    public async Task Changing_settings_can_be_undone_and_redone()
    {
        var app = await _app.StartAsync();
        var board = await NewBoardAsync(app);
        var list = board.Lists[0];

        await SaveSettingsAsync(list, name: "Backlog", isDone: true, wip: 3, setWip: true);
        Assert.Equal("리스트 설정 변경", app.History.NextUndo!.Description);

        await app.UndoCommand.ExecuteAsync(null);
        Assert.Equal("To Do", list.Name);
        Assert.False(list.IsDoneList);
        Assert.Null(list.WipLimit);
        var reloaded = (await ReloadBoardAsync("T")).Lists[0];
        Assert.Equal("To Do", reloaded.Name);
        Assert.Null(reloaded.WipLimit);

        await app.RedoCommand.ExecuteAsync(null);
        Assert.Equal("Backlog", list.Name);
        Assert.True(list.IsDoneList);
        Assert.Equal(3, list.WipLimit);
        Assert.Equal(3, (await ReloadBoardAsync("T")).Lists[0].WipLimit);
    }

    [Fact]
    public async Task Saving_without_changes_is_not_recorded()
    {
        var app = await _app.StartAsync();
        var board = await NewBoardAsync(app);

        await SaveSettingsAsync(board.Lists[0]);   // 열고 아무것도 안 바꾸고 저장

        Assert.False(app.History.CanUndo);
    }

    [Fact]
    public async Task A_deleted_list_comes_back_with_its_settings()
    {
        var app = await _app.StartAsync();
        var board = await NewBoardAsync(app);
        var list = board.Lists[1];
        await SaveSettingsAsync(list, name: "Review", isDone: true, wip: 5, setWip: true);
        app.History.Clear();

        await list.DeleteCommand.ExecuteAsync(null);
        await app.UndoCommand.ExecuteAsync(null);

        Assert.Same(list, board.Lists[1]);
        Assert.Equal("Review", list.Name);
        Assert.True(list.IsDoneList);
        Assert.Equal(5, list.WipLimit);

        var restored = (await ReloadBoardAsync("T")).Lists[1];
        Assert.Equal("Review", restored.Name);
        Assert.True(restored.IsDoneList);
        Assert.Equal(5, restored.WipLimit);
    }
}
