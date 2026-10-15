using Kanban.Core;
using Kanban.Tests.Support;

namespace Kanban.Tests;

public sealed class BoardRepositoryTests : IDisposable
{
    private readonly TestApp _app = new();

    public void Dispose() => _app.Dispose();

    // 마이그레이션이 만들어 두는 기본 보드. 대부분의 테스트는 이 보드 안에서 동작한다.
    private static async Task<int> BoardIdAsync(IBoardRepository repository) =>
        (await repository.GetBoardsAsync())[0].Id;

    private async Task<IBoardRepository> CreateInitializedAsync()
    {
        var repository = _app.CreateRepository();
        await repository.InitializeAsync();
        return repository;
    }

    [Fact]
    public async Task New_database_has_no_lists()
    {
        var repository = await CreateInitializedAsync();

        Assert.Empty(await repository.GetListsAsync(await BoardIdAsync(repository)));
    }

    [Fact]
    public async Task Seeds_default_labels()
    {
        var repository = await CreateInitializedAsync();

        var labels = await repository.GetLabelsAsync();

        Assert.Equal(6, labels.Count);
        Assert.All(labels, l => Assert.StartsWith("#", l.Color));
    }

    [Fact]
    public async Task Lists_and_cards_get_increasing_positions()
    {
        var repository = await CreateInitializedAsync();

        var first = await repository.AddListAsync(await BoardIdAsync(repository), "To Do");
        var second = await repository.AddListAsync(await BoardIdAsync(repository), "Done");
        var a = await repository.AddCardAsync(first.Id, "A", null);
        var b = await repository.AddCardAsync(first.Id, "B", null);
        var other = await repository.AddCardAsync(second.Id, "X", null);

        Assert.Equal(0, first.Position);
        Assert.Equal(1, second.Position);
        Assert.Equal(0, a.Position);
        Assert.Equal(1, b.Position);
        Assert.Equal(0, other.Position); // 위치는 리스트마다 따로 센다.
    }

    [Fact]
    public async Task Data_survives_reopening_the_database()
    {
        var repository = await CreateInitializedAsync();
        var list = await repository.AddListAsync(await BoardIdAsync(repository), "To Do");
        await repository.AddCardAsync(list.Id, "A", "desc");
        await repository.AddCardAsync(list.Id, "B", null);

        var reopened = await CreateInitializedAsync();
        var lists = await reopened.GetListsAsync(await BoardIdAsync(reopened));

        var only = Assert.Single(lists);
        Assert.Equal(new[] { "A", "B" }, only.Cards.Select(c => c.Title));
        Assert.Equal("desc", only.Cards[0].Description);
    }

    [Fact]
    public async Task SaveCardOrder_moves_cards_between_lists_and_reorders()
    {
        var repository = await CreateInitializedAsync();
        var todo = await repository.AddListAsync(await BoardIdAsync(repository), "To Do");
        var done = await repository.AddListAsync(await BoardIdAsync(repository), "Done");
        var a = await repository.AddCardAsync(todo.Id, "A", null);
        var b = await repository.AddCardAsync(todo.Id, "B", null);
        var c = await repository.AddCardAsync(todo.Id, "C", null);

        await repository.SaveCardOrderAsync(new[]
        {
            new ListCardOrder(todo.Id, new[] { b.Id, a.Id }),
            new ListCardOrder(done.Id, new[] { c.Id }),
        });

        var lists = await repository.GetListsAsync(await BoardIdAsync(repository));
        Assert.Equal(new[] { "B", "A" }, lists[0].Cards.Select(x => x.Title));
        Assert.Equal(new[] { "C" }, lists[1].Cards.Select(x => x.Title));
    }

    [Fact]
    public async Task UpdateCard_saves_title_description_and_due_date()
    {
        var repository = await CreateInitializedAsync();
        var list = await repository.AddListAsync(await BoardIdAsync(repository), "To Do");
        var card = await repository.AddCardAsync(list.Id, "A", null);

        card.Title = "A2";
        card.Description = "new";
        card.DueDate = new DateOnly(2030, 5, 17);
        await repository.UpdateCardAsync(card);

        var saved = (await repository.GetListsAsync(await BoardIdAsync(repository)))[0].Cards[0];
        Assert.Equal("A2", saved.Title);
        Assert.Equal("new", saved.Description);
        Assert.Equal(new DateOnly(2030, 5, 17), saved.DueDate);
    }

    [Fact]
    public async Task DeleteList_removes_its_cards()
    {
        var repository = await CreateInitializedAsync();
        var list = await repository.AddListAsync(await BoardIdAsync(repository), "To Do");
        var card = await repository.AddCardAsync(list.Id, "A", null);
        await repository.AddChecklistItemAsync(card.Id, "item");

        await repository.DeleteListAsync(list.Id);

        Assert.Empty(await repository.GetListsAsync(await BoardIdAsync(repository)));
    }

    [Fact]
    public async Task SetCardLabels_replaces_the_label_set()
    {
        var repository = await CreateInitializedAsync();
        var list = await repository.AddListAsync(await BoardIdAsync(repository), "To Do");
        var card = await repository.AddCardAsync(list.Id, "A", null);

        await repository.SetCardLabelsAsync(card.Id, new[] { 1, 3 });
        await repository.SetCardLabelsAsync(card.Id, new[] { 3, 5 });

        var saved = (await repository.GetListsAsync(await BoardIdAsync(repository)))[0].Cards[0];
        Assert.Equal(new[] { 3, 5 }, saved.Labels.Select(l => l.Id));
    }

    [Fact]
    public async Task Checklist_items_can_be_added_checked_and_deleted()
    {
        var repository = await CreateInitializedAsync();
        var list = await repository.AddListAsync(await BoardIdAsync(repository), "To Do");
        var card = await repository.AddCardAsync(list.Id, "A", null);

        var one = await repository.AddChecklistItemAsync(card.Id, "one");
        var two = await repository.AddChecklistItemAsync(card.Id, "two");
        await repository.SetChecklistItemDoneAsync(one.Id, true);
        await repository.DeleteChecklistItemAsync(two.Id);

        var items = (await repository.GetListsAsync(await BoardIdAsync(repository)))[0].Cards[0].ChecklistItems;
        var only = Assert.Single(items);
        Assert.Equal("one", only.Text);
        Assert.True(only.IsDone);
    }

    // ----- 보드 -----

    [Fact]
    public async Task Migration_creates_a_default_board()
    {
        var repository = await CreateInitializedAsync();

        var boards = await repository.GetBoardsAsync();

        var only = Assert.Single(boards);
        Assert.Equal("My Kanban Board", only.Name);
    }

    [Fact]
    public async Task Boards_get_increasing_positions_and_keep_their_own_lists()
    {
        var repository = await CreateInitializedAsync();
        var first = (await repository.GetBoardsAsync())[0];
        var second = await repository.AddBoardAsync("Side project");
        await repository.AddListAsync(first.Id, "To Do");
        await repository.AddListAsync(second.Id, "Backlog");

        var firstLists = await repository.GetListsAsync(first.Id);
        var secondLists = await repository.GetListsAsync(second.Id);

        Assert.Equal(1, second.Position);
        Assert.Equal(new[] { "To Do" }, firstLists.Select(l => l.Name));
        Assert.Equal(new[] { "Backlog" }, secondLists.Select(l => l.Name));
        Assert.Equal(0, secondLists[0].Position); // 리스트 위치는 보드마다 따로 센다.
    }

    [Fact]
    public async Task Board_can_be_renamed()
    {
        var repository = await CreateInitializedAsync();
        var board = (await repository.GetBoardsAsync())[0];

        await repository.RenameBoardAsync(board.Id, "Renamed");

        Assert.Equal("Renamed", (await repository.GetBoardsAsync())[0].Name);
    }

    [Fact]
    public async Task Deleting_a_board_removes_its_lists_and_cards_but_not_other_boards()
    {
        var repository = await CreateInitializedAsync();
        var keep = (await repository.GetBoardsAsync())[0];
        var remove = await repository.AddBoardAsync("Temp");
        var keepList = await repository.AddListAsync(keep.Id, "Keep");
        await repository.AddCardAsync(keepList.Id, "kept", null);
        var removeList = await repository.AddListAsync(remove.Id, "Gone");
        var card = await repository.AddCardAsync(removeList.Id, "gone", null);
        await repository.AddChecklistItemAsync(card.Id, "item");
        await repository.SetCardLabelsAsync(card.Id, new[] { 1 });

        await repository.DeleteBoardAsync(remove.Id);

        var boards = await repository.GetBoardsAsync();
        Assert.Equal(new[] { keep.Id }, boards.Select(b => b.Id));
        var lists = await repository.GetListsAsync(keep.Id);
        Assert.Equal("kept", Assert.Single(Assert.Single(lists).Cards).Title);
        Assert.Empty(await repository.GetListsAsync(remove.Id));
    }
}
