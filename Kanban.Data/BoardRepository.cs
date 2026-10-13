using Kanban.Core;
using Kanban.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Data;

/// <summary>
/// EF Core(SQLite) 기반 저장소. DbContext는 메서드마다 짧게 만들어 쓰고 버린다.
/// (데스크톱 앱에서는 DbContext를 오래 들고 있지 않는 것이 안전하다.)
/// </summary>
public sealed class BoardRepository : IBoardRepository
{
    private readonly IDbContextFactory<KanbanDbContext> _factory;

    public BoardRepository(IDbContextFactory<KanbanDbContext> factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        await db.Database.MigrateAsync(ct);
    }

    public async Task<IReadOnlyList<BoardList>> GetListsAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var lists = await db.Lists
            .AsNoTracking()
            .Include(l => l.Cards.OrderBy(c => c.Position))
                .ThenInclude(c => c.Labels.OrderBy(x => x.Id))
            .Include(l => l.Cards)
                .ThenInclude(c => c.ChecklistItems.OrderBy(i => i.Position))
            .OrderBy(l => l.Position)
            .ToListAsync(ct);
        return lists;
    }

    public async Task<IReadOnlyList<Label>> GetLabelsAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        return await db.Labels.AsNoTracking().OrderBy(l => l.Id).ToListAsync(ct);
    }

    public async Task<BoardList> AddListAsync(string name, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var nextPosition = await db.Lists.AnyAsync(ct)
            ? await db.Lists.MaxAsync(l => l.Position, ct) + 1
            : 0;

        var list = new BoardList { Name = name, Position = nextPosition };
        db.Lists.Add(list);
        await db.SaveChangesAsync(ct);
        return list;
    }

    public async Task DeleteListAsync(int listId, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        // 카드는 FK의 Cascade 설정으로 함께 삭제된다.
        await db.Lists.Where(l => l.Id == listId).ExecuteDeleteAsync(ct);
    }

    public async Task<Card> AddCardAsync(int listId, string title, string? description, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var nextPosition = await db.Cards.Where(c => c.ListId == listId).AnyAsync(ct)
            ? await db.Cards.Where(c => c.ListId == listId).MaxAsync(c => c.Position, ct) + 1
            : 0;

        var card = new Card
        {
            ListId = listId,
            Title = title,
            Description = description,
            Position = nextPosition,
        };
        db.Cards.Add(card);
        await db.SaveChangesAsync(ct);
        return card;
    }

    public async Task UpdateCardAsync(Card card, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        await db.Cards
            .Where(c => c.Id == card.Id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.Title, card.Title)
                .SetProperty(c => c.Description, card.Description)
                .SetProperty(c => c.DueDate, card.DueDate), ct);
    }

    public async Task SetCardLabelsAsync(int cardId, IReadOnlyCollection<int> labelIds, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var card = await db.Cards.Include(c => c.Labels).FirstOrDefaultAsync(c => c.Id == cardId, ct);
        if (card is null)
            return;

        var labels = await db.Labels.Where(l => labelIds.Contains(l.Id)).ToListAsync(ct);
        card.Labels.Clear();
        card.Labels.AddRange(labels);
        await db.SaveChangesAsync(ct);
    }

    public async Task<ChecklistItem> AddChecklistItemAsync(int cardId, string text, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var items = db.ChecklistItems.Where(i => i.CardId == cardId);
        var nextPosition = await items.AnyAsync(ct) ? await items.MaxAsync(i => i.Position, ct) + 1 : 0;

        var item = new ChecklistItem { CardId = cardId, Text = text, Position = nextPosition };
        db.ChecklistItems.Add(item);
        await db.SaveChangesAsync(ct);
        return item;
    }

    public async Task SetChecklistItemDoneAsync(int itemId, bool isDone, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        await db.ChecklistItems
            .Where(i => i.Id == itemId)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.IsDone, isDone), ct);
    }

    public async Task DeleteChecklistItemAsync(int itemId, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        await db.ChecklistItems.Where(i => i.Id == itemId).ExecuteDeleteAsync(ct);
    }

    public async Task DeleteCardAsync(int cardId, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        await db.Cards.Where(c => c.Id == cardId).ExecuteDeleteAsync(ct);
    }

    public async Task SaveCardOrderAsync(IEnumerable<ListCardOrder> orders, CancellationToken ct = default)
    {
        var orderList = orders.ToList();
        var ids = orderList.SelectMany(o => o.CardIds).ToList();

        await using var db = await _factory.CreateDbContextAsync(ct);
        var cards = await db.Cards.Where(c => ids.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);

        foreach (var order in orderList)
        {
            for (var i = 0; i < order.CardIds.Count; i++)
            {
                if (cards.TryGetValue(order.CardIds[i], out var card))
                {
                    card.ListId = order.ListId;
                    card.Position = i;
                }
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
