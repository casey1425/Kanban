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

    public async Task<IReadOnlyList<Board>> GetBoardsAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        return await db.Boards.AsNoTracking().OrderBy(b => b.Position).ThenBy(b => b.Id).ToListAsync(ct);
    }

    public async Task<Board> AddBoardAsync(string name, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var nextPosition = await db.Boards.AnyAsync(ct)
            ? await db.Boards.MaxAsync(b => b.Position, ct) + 1
            : 0;

        var board = new Board { Name = name, Position = nextPosition };
        db.Boards.Add(board);
        await db.SaveChangesAsync(ct);
        return board;
    }

    public async Task RenameBoardAsync(int boardId, string name, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        await db.Boards
            .Where(b => b.Id == boardId)
            .ExecuteUpdateAsync(s => s.SetProperty(b => b.Name, name), ct);
    }

    public async Task DeleteBoardAsync(int boardId, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        // 리스트와 카드(그리고 체크리스트, 라벨 연결)는 FK의 Cascade 설정으로 함께 삭제된다.
        await db.Boards.Where(b => b.Id == boardId).ExecuteDeleteAsync(ct);
    }

    public async Task<IReadOnlyList<BoardList>> GetListsAsync(int boardId, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var lists = await db.Lists
            .AsNoTracking()
            .Where(l => l.BoardId == boardId)
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

    public async Task<BoardList> AddListAsync(int boardId, string name, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var inBoard = db.Lists.Where(l => l.BoardId == boardId);
        var nextPosition = await inBoard.AnyAsync(ct)
            ? await inBoard.MaxAsync(l => l.Position, ct) + 1
            : 0;

        var list = new BoardList { BoardId = boardId, Name = name, Position = nextPosition };
        db.Lists.Add(list);
        await db.SaveChangesAsync(ct);
        return list;
    }

    public async Task RestoreListAsync(BoardList list, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var labels = await LoadLabelsAsync(db, list.Cards.SelectMany(c => c.Labels.Select(l => l.Id)), ct);

        var entity = new BoardList
        {
            Id = list.Id,
            BoardId = list.BoardId,
            Name = list.Name,
            Position = list.Position,
            Cards = list.Cards.Select(c => CloneForInsert(c, labels)).ToList(),
        };

        db.Lists.Add(entity);
        await db.SaveChangesAsync(ct);
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

    public async Task RestoreCardAsync(Card card, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var labels = await LoadLabelsAsync(db, card.Labels.Select(l => l.Id), ct);

        db.Cards.Add(CloneForInsert(card, labels));
        await db.SaveChangesAsync(ct);
    }

    private static async Task<Dictionary<int, Label>> LoadLabelsAsync(
        KanbanDbContext db, IEnumerable<int> labelIds, CancellationToken ct)
    {
        var ids = labelIds.Distinct().ToList();
        return await db.Labels.Where(l => ids.Contains(l.Id)).ToDictionaryAsync(l => l.Id, ct);
    }

    // 스냅샷을 DB에 넣을 새 엔티티로 복사한다. Id를 그대로 지정하면 EF가 그 값으로 삽입한다.
    private static Card CloneForInsert(Card source, IReadOnlyDictionary<int, Label> labels) => new()
    {
        Id = source.Id,
        ListId = source.ListId,
        Title = source.Title,
        Description = source.Description,
        DueDate = source.DueDate,
        Position = source.Position,
        Labels = source.Labels.Where(l => labels.ContainsKey(l.Id)).Select(l => labels[l.Id]).ToList(),
        ChecklistItems = source.ChecklistItems.Select(i => new ChecklistItem
        {
            Id = i.Id,
            Text = i.Text,
            IsDone = i.IsDone,
            Position = i.Position,
        }).ToList(),
    };

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

    public async Task RestoreChecklistItemAsync(ChecklistItem item, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        db.ChecklistItems.Add(new ChecklistItem
        {
            Id = item.Id,
            CardId = item.CardId,
            Text = item.Text,
            IsDone = item.IsDone,
            Position = item.Position,
        });
        await db.SaveChangesAsync(ct);
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
