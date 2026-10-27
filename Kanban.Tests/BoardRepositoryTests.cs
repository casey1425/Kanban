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

    // ----- 복원 (실행 취소용) -----

    private static Kanban.Core.Models.Card Snapshot(Kanban.Core.Models.Card card, IEnumerable<int> labelIds) => new()
    {
        Id = card.Id,
        ListId = card.ListId,
        Title = card.Title,
        Description = card.Description,
        DueDate = card.DueDate,
        Position = card.Position,
        Labels = labelIds.Select(id => new Kanban.Core.Models.Label { Id = id }).ToList(),
        ChecklistItems = card.ChecklistItems.Select(i => new Kanban.Core.Models.ChecklistItem
        {
            Id = i.Id, Text = i.Text, IsDone = i.IsDone, Position = i.Position,
        }).ToList(),
    };

    [Fact]
    public async Task Deleted_card_is_restored_with_the_same_id_labels_checklist_and_due_date()
    {
        var repository = await CreateInitializedAsync();
        var list = await repository.AddListAsync(await BoardIdAsync(repository), "To Do");
        var card = await repository.AddCardAsync(list.Id, "A", "desc");
        card.DueDate = new DateOnly(2031, 4, 5);
        await repository.UpdateCardAsync(card);
        await repository.SetCardLabelsAsync(card.Id, new[] { 2, 4 });
        var one = await repository.AddChecklistItemAsync(card.Id, "one");
        await repository.SetChecklistItemDoneAsync(one.Id, true);

        var saved = (await repository.GetListsAsync(await BoardIdAsync(repository)))[0].Cards[0];
        var snapshot = Snapshot(saved, saved.Labels.Select(l => l.Id));

        await repository.DeleteCardAsync(card.Id);
        Assert.Empty((await repository.GetListsAsync(await BoardIdAsync(repository)))[0].Cards);

        await repository.RestoreCardAsync(snapshot);

        var restored = Assert.Single((await repository.GetListsAsync(await BoardIdAsync(repository)))[0].Cards);
        Assert.Equal(card.Id, restored.Id);
        Assert.Equal("A", restored.Title);
        Assert.Equal("desc", restored.Description);
        Assert.Equal(new DateOnly(2031, 4, 5), restored.DueDate);
        Assert.Equal(new[] { 2, 4 }, restored.Labels.Select(l => l.Id));
        var item = Assert.Single(restored.ChecklistItems);
        Assert.Equal(one.Id, item.Id);
        Assert.True(item.IsDone);
    }

    [Fact]
    public async Task Restoring_a_card_does_not_collide_with_cards_created_in_between()
    {
        var repository = await CreateInitializedAsync();
        var list = await repository.AddListAsync(await BoardIdAsync(repository), "To Do");
        var original = await repository.AddCardAsync(list.Id, "original", null);
        var snapshot = Snapshot(original, Array.Empty<int>());

        await repository.DeleteCardAsync(original.Id);
        var newer = await repository.AddCardAsync(list.Id, "newer", null);   // 삭제한 Id를 재사용하지 않아야 한다.

        await repository.RestoreCardAsync(snapshot);

        Assert.NotEqual(original.Id, newer.Id);
        var ids = (await repository.GetListsAsync(await BoardIdAsync(repository)))[0].Cards.Select(c => c.Id).ToList();
        Assert.Contains(original.Id, ids);
        Assert.Contains(newer.Id, ids);
    }

    [Fact]
    public async Task Deleted_list_is_restored_with_its_cards_at_the_same_ids()
    {
        var repository = await CreateInitializedAsync();
        var boardId = await BoardIdAsync(repository);
        var list = await repository.AddListAsync(boardId, "Doomed");
        var a = await repository.AddCardAsync(list.Id, "A", null);
        var b = await repository.AddCardAsync(list.Id, "B", null);
        await repository.SetCardLabelsAsync(a.Id, new[] { 1 });
        await repository.AddChecklistItemAsync(b.Id, "item");

        var saved = (await repository.GetListsAsync(boardId))[0];
        var snapshot = new Kanban.Core.Models.BoardList
        {
            Id = saved.Id, BoardId = saved.BoardId, Name = saved.Name, Position = saved.Position,
            Cards = saved.Cards.Select(c => Snapshot(c, c.Labels.Select(l => l.Id))).ToList(),
        };

        await repository.DeleteListAsync(list.Id);
        Assert.Empty(await repository.GetListsAsync(boardId));

        await repository.RestoreListAsync(snapshot);

        var restored = Assert.Single(await repository.GetListsAsync(boardId));
        Assert.Equal(list.Id, restored.Id);
        Assert.Equal("Doomed", restored.Name);
        Assert.Equal(new[] { a.Id, b.Id }, restored.Cards.Select(c => c.Id));
        Assert.Equal(new[] { 1 }, restored.Cards[0].Labels.Select(l => l.Id));
        Assert.Equal("item", Assert.Single(restored.Cards[1].ChecklistItems).Text);
    }

    [Fact]
    public async Task Deleted_checklist_item_is_restored_with_the_same_id_state_and_position()
    {
        var repository = await CreateInitializedAsync();
        var list = await repository.AddListAsync(await BoardIdAsync(repository), "To Do");
        var card = await repository.AddCardAsync(list.Id, "A", null);
        var first = await repository.AddChecklistItemAsync(card.Id, "first");
        var second = await repository.AddChecklistItemAsync(card.Id, "second");
        await repository.SetChecklistItemDoneAsync(first.Id, true);
        var snapshot = new Kanban.Core.Models.ChecklistItem
        {
            Id = first.Id, CardId = card.Id, Text = "first", IsDone = true, Position = first.Position,
        };

        await repository.DeleteChecklistItemAsync(first.Id);
        var newer = await repository.AddChecklistItemAsync(card.Id, "newer");   // 삭제한 Id를 재사용하면 안 된다
        await repository.RestoreChecklistItemAsync(snapshot);

        var items = (await repository.GetListsAsync(await BoardIdAsync(repository)))[0].Cards[0].ChecklistItems;
        Assert.NotEqual(first.Id, newer.Id);
        var restored = items.Single(i => i.Id == first.Id);
        Assert.Equal("first", restored.Text);
        Assert.True(restored.IsDone);
        Assert.Equal(first.Position, restored.Position);
        Assert.Contains(items, i => i.Id == second.Id);
    }
}
