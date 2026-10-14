using Kanban.Core;
using Kanban.Tests.Support;

namespace Kanban.Tests;

public sealed class BoardRepositoryTests : IDisposable
{
    private readonly TestApp _app = new();

    public void Dispose() => _app.Dispose();

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

        Assert.Empty(await repository.GetListsAsync());
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

        var first = await repository.AddListAsync("To Do");
        var second = await repository.AddListAsync("Done");
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
        var list = await repository.AddListAsync("To Do");
        await repository.AddCardAsync(list.Id, "A", "desc");
        await repository.AddCardAsync(list.Id, "B", null);

        var reopened = await CreateInitializedAsync();
        var lists = await reopened.GetListsAsync();

        var only = Assert.Single(lists);
        Assert.Equal(new[] { "A", "B" }, only.Cards.Select(c => c.Title));
        Assert.Equal("desc", only.Cards[0].Description);
    }

    [Fact]
    public async Task SaveCardOrder_moves_cards_between_lists_and_reorders()
    {
        var repository = await CreateInitializedAsync();
        var todo = await repository.AddListAsync("To Do");
        var done = await repository.AddListAsync("Done");
        var a = await repository.AddCardAsync(todo.Id, "A", null);
        var b = await repository.AddCardAsync(todo.Id, "B", null);
        var c = await repository.AddCardAsync(todo.Id, "C", null);

        await repository.SaveCardOrderAsync(new[]
        {
            new ListCardOrder(todo.Id, new[] { b.Id, a.Id }),
            new ListCardOrder(done.Id, new[] { c.Id }),
        });

        var lists = await repository.GetListsAsync();
        Assert.Equal(new[] { "B", "A" }, lists[0].Cards.Select(x => x.Title));
        Assert.Equal(new[] { "C" }, lists[1].Cards.Select(x => x.Title));
    }

    [Fact]
    public async Task UpdateCard_saves_title_description_and_due_date()
    {
        var repository = await CreateInitializedAsync();
        var list = await repository.AddListAsync("To Do");
        var card = await repository.AddCardAsync(list.Id, "A", null);

        card.Title = "A2";
        card.Description = "new";
        card.DueDate = new DateOnly(2030, 5, 17);
        await repository.UpdateCardAsync(card);

        var saved = (await repository.GetListsAsync())[0].Cards[0];
        Assert.Equal("A2", saved.Title);
        Assert.Equal("new", saved.Description);
        Assert.Equal(new DateOnly(2030, 5, 17), saved.DueDate);
    }

    [Fact]
    public async Task DeleteList_removes_its_cards()
    {
        var repository = await CreateInitializedAsync();
        var list = await repository.AddListAsync("To Do");
        var card = await repository.AddCardAsync(list.Id, "A", null);
        await repository.AddChecklistItemAsync(card.Id, "item");

        await repository.DeleteListAsync(list.Id);

        Assert.Empty(await repository.GetListsAsync());
    }

    [Fact]
    public async Task SetCardLabels_replaces_the_label_set()
    {
        var repository = await CreateInitializedAsync();
        var list = await repository.AddListAsync("To Do");
        var card = await repository.AddCardAsync(list.Id, "A", null);

        await repository.SetCardLabelsAsync(card.Id, new[] { 1, 3 });
        await repository.SetCardLabelsAsync(card.Id, new[] { 3, 5 });

        var saved = (await repository.GetListsAsync())[0].Cards[0];
        Assert.Equal(new[] { 3, 5 }, saved.Labels.Select(l => l.Id));
    }

    [Fact]
    public async Task Checklist_items_can_be_added_checked_and_deleted()
    {
        var repository = await CreateInitializedAsync();
        var list = await repository.AddListAsync("To Do");
        var card = await repository.AddCardAsync(list.Id, "A", null);

        var one = await repository.AddChecklistItemAsync(card.Id, "one");
        var two = await repository.AddChecklistItemAsync(card.Id, "two");
        await repository.SetChecklistItemDoneAsync(one.Id, true);
        await repository.DeleteChecklistItemAsync(two.Id);

        var items = (await repository.GetListsAsync())[0].Cards[0].ChecklistItems;
        var only = Assert.Single(items);
        Assert.Equal("one", only.Text);
        Assert.True(only.IsDone);
    }
}
