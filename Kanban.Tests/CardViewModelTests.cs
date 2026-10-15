using Kanban.App.ViewModels;
using Kanban.Tests.Support;

namespace Kanban.Tests;

/// <summary>카드 상세(마감일, 라벨, 체크리스트)와 검색/필터.</summary>
public sealed class CardViewModelTests : IDisposable
{
    private readonly TestApp _app = new();

    public void Dispose() => _app.Dispose();

    private static async Task AddCardAsync(BoardListViewModel list, string title)
    {
        list.NewCardTitle = title;
        await list.AddNewCardCommand.ExecuteAsync(null);
    }

    // ----- 마감일 / 상세 -----

    [Fact]
    public async Task Due_date_is_saved_when_detail_closes()
    {
        var app = await _app.StartAsync();
        var card = app.SelectedBoard!.Lists[0].Cards[0];

        card.OpenDetailCommand.Execute(null);
        card.DueDateTime = new DateTime(2020, 1, 15);
        await app.Detail!.CloseCommand.ExecuteAsync(null);

        var restarted = await _app.StartAsync();
        Assert.Equal(new DateOnly(2020, 1, 15), restarted.SelectedBoard!.Lists[0].Cards[0].DueDate);
        Assert.True(restarted.SelectedBoard!.Lists[0].Cards[0].IsOverdue);
    }

    [Fact]
    public async Task Clearing_due_date_removes_it()
    {
        var app = await _app.StartAsync();
        var card = app.SelectedBoard!.Lists[0].Cards[0];
        card.DueDate = new DateOnly(2030, 1, 1);

        card.OpenDetailCommand.Execute(null);
        app.Detail!.ClearDueDateCommand.Execute(null);
        await app.Detail.CloseCommand.ExecuteAsync(null);

        var restarted = await _app.StartAsync();
        Assert.Null(restarted.SelectedBoard!.Lists[0].Cards[0].DueDate);
        Assert.False(restarted.SelectedBoard!.Lists[0].Cards[0].HasDueDate);
    }

    [Fact]
    public async Task Blank_title_is_restored_and_blank_description_becomes_null()
    {
        var app = await _app.StartAsync();
        var card = app.SelectedBoard!.Lists[0].Cards[0];
        var original = card.Title;

        card.OpenDetailCommand.Execute(null);
        card.Title = "";
        card.Description = "   ";
        await app.Detail!.CloseCommand.ExecuteAsync(null);

        Assert.Equal(original, card.Title);
        var restarted = await _app.StartAsync();
        Assert.Null(restarted.SelectedBoard!.Lists[0].Cards[0].Description);
    }

    // ----- 라벨 -----

    [Fact]
    public async Task Labels_can_be_toggled_and_are_persisted()
    {
        var app = await _app.StartAsync();
        var card = app.SelectedBoard!.Lists[0].Cards[0];
        Assert.Equal(6, card.LabelOptions.Count);
        Assert.False(card.HasLabels);

        await card.LabelOptions[0].ToggleCommand.ExecuteAsync(null);
        await card.LabelOptions[2].ToggleCommand.ExecuteAsync(null);
        await card.LabelOptions[3].ToggleCommand.ExecuteAsync(null);
        await card.LabelOptions[3].ToggleCommand.ExecuteAsync(null); // 다시 눌러 해제

        Assert.Equal(new[] { "긴급", "기능" }, card.Labels.Select(l => l.Name));

        var restarted = await _app.StartAsync();
        var restored = restarted.SelectedBoard!.Lists[0].Cards[0];
        Assert.Equal(new[] { 1, 3 }, restored.LabelOptions.Where(o => o.IsSelected).Select(o => o.Id));
    }

    [Fact]
    public async Task Newly_added_cards_also_have_label_options()
    {
        var app = await _app.StartAsync();

        await AddCardAsync(app.SelectedBoard!.Lists[0], "new");

        Assert.Equal(6, app.SelectedBoard!.Lists[0].Cards.Last().LabelOptions.Count);
    }

    // ----- 체크리스트 -----

    [Fact]
    public async Task Checklist_progress_is_tracked_and_persisted()
    {
        var app = await _app.StartAsync();
        var card = app.SelectedBoard!.Lists[0].Cards[0];
        Assert.Equal("0/0", card.ChecklistSummary);

        foreach (var text in new[] { "one", "two", "three" })
        {
            card.NewChecklistText = text;
            await card.AddChecklistItemCommand.ExecuteAsync(null);
        }
        Assert.Equal("0/3", card.ChecklistSummary);

        // 체크박스 클릭: 바인딩이 IsDone을 바꾼 뒤 Command가 실행된다.
        card.Checklist[0].IsDone = true;
        await card.Checklist[0].ToggleCommand.ExecuteAsync(null);
        card.Checklist[2].IsDone = true;
        await card.Checklist[2].ToggleCommand.ExecuteAsync(null);
        Assert.Equal("2/3", card.ChecklistSummary);
        Assert.False(card.IsChecklistComplete);

        await card.Checklist[1].DeleteCommand.ExecuteAsync(null);
        Assert.Equal("2/2", card.ChecklistSummary);
        Assert.True(card.IsChecklistComplete);

        var restarted = await _app.StartAsync();
        var restored = restarted.SelectedBoard!.Lists[0].Cards[0];
        Assert.Equal(new[] { "one:True", "three:True" }, restored.Checklist.Select(i => $"{i.Text}:{i.IsDone}"));
    }

    [Fact]
    public async Task Blank_checklist_input_cannot_be_added()
    {
        var app = await _app.StartAsync();
        var card = app.SelectedBoard!.Lists[0].Cards[0];

        card.NewChecklistText = "  ";

        Assert.False(card.AddChecklistItemCommand.CanExecute(null));
    }

    // ----- 검색 / 필터 -----

    private static string Visible(BoardListViewModel list) =>
        string.Join("|", list.Cards.Where(c => c.IsMatch).Select(c => c.Title));

    private async Task<(MainWindowViewModel App, BoardListViewModel Todo)> CreateFilterBoardAsync()
    {
        var app = await _app.StartAsync();
        var todo = app.SelectedBoard!.Lists[0];
        await todo.Cards[0].DeleteCommand.ExecuteAsync(null); // 안내 카드는 검색 결과를 흐리므로 지운다.

        foreach (var title in new[] { "Apple pie", "banana split", "Cherry" })
            await AddCardAsync(todo, title);

        return (app, todo);
    }

    [Fact]
    public async Task Search_is_case_insensitive_and_matches_substrings()
    {
        var (app, todo) = await CreateFilterBoardAsync();

        app.SelectedBoard!.SearchText = "APPLE";
        Assert.Equal("Apple pie", Visible(todo));

        app.SelectedBoard!.SearchText = "an";
        Assert.Equal("banana split", Visible(todo));
    }

    [Fact]
    public async Task Search_also_looks_into_checklist_items()
    {
        var (app, todo) = await CreateFilterBoardAsync();
        var cherry = todo.Cards.First(c => c.Title == "Cherry");
        cherry.NewChecklistText = "buy sugar";
        await cherry.AddChecklistItemCommand.ExecuteAsync(null);

        app.SelectedBoard!.SearchText = "sugar";

        Assert.Equal("Cherry", Visible(todo));
    }

    [Fact]
    public async Task Label_filter_combines_with_search_and_tracks_label_changes()
    {
        var (app, todo) = await CreateFilterBoardAsync();
        var apple = todo.Cards.First(c => c.Title == "Apple pie");
        await apple.LabelOptions[0].ToggleCommand.ExecuteAsync(null); // 긴급

        app.SelectedBoard!.SelectedLabelFilter = app.SelectedBoard!.LabelFilters.First(f => f.LabelId == 1);
        Assert.Equal("Apple pie", Visible(todo));

        app.SelectedBoard!.SearchText = "banana";
        Assert.Equal("", Visible(todo)); // 라벨 AND 검색

        app.SelectedBoard!.SearchText = "";
        await apple.LabelOptions[0].ToggleCommand.ExecuteAsync(null); // 라벨 해제
        Assert.Equal("", Visible(todo));
    }

    [Fact]
    public async Task Clearing_the_filter_shows_everything()
    {
        var (app, todo) = await CreateFilterBoardAsync();
        app.SelectedBoard!.SearchText = "zzz";
        Assert.Equal("", Visible(todo));

        app.SelectedBoard!.ClearFilterCommand.Execute(null);

        Assert.True(todo.Cards.All(c => c.IsMatch));
        Assert.Null(app.SelectedBoard!.SelectedLabelFilter?.LabelId);
    }

    [Fact]
    public async Task Moved_cards_keep_matching_the_active_filter()
    {
        var (app, todo) = await CreateFilterBoardAsync();
        app.SelectedBoard!.SearchText = "apple";

        // 필터가 켜진 상태에서 카드를 다른 리스트로 옮겨도 일치 여부가 그대로 유지된다.
        var banana = todo.Cards.First(c => c.Title == "banana split");
        await app.SelectedBoard!.Lists[1].MoveCardHereAsync(banana, 0);

        Assert.False(banana.IsMatch);
        Assert.Equal("Apple pie", Visible(todo));
    }
}
