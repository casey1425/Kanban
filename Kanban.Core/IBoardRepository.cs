using Kanban.Core.Models;

namespace Kanban.Core;

/// <summary>보드 데이터의 저장소. UI는 이 인터페이스만 알고, 실제 저장 방식(SQLite 등)은 모른다.</summary>
public interface IBoardRepository
{
    /// <summary>저장소를 준비한다(DB 생성/마이그레이션). 다른 메서드보다 먼저 한 번 호출한다.</summary>
    Task InitializeAsync(CancellationToken ct = default);

    /// <summary>모든 리스트를 순서대로, 각 리스트의 카드도 순서대로 가져온다.</summary>
    Task<IReadOnlyList<BoardList>> GetListsAsync(CancellationToken ct = default);

    Task<BoardList> AddListAsync(string name, CancellationToken ct = default);

    Task DeleteListAsync(int listId, CancellationToken ct = default);

    Task<Card> AddCardAsync(int listId, string title, string? description, CancellationToken ct = default);

    Task UpdateCardAsync(Card card, CancellationToken ct = default);

    Task DeleteCardAsync(int cardId, CancellationToken ct = default);

    /// <summary>주어진 리스트들에 대해 카드의 소속 리스트와 순서를 한 번에 저장한다.</summary>
    Task SaveCardOrderAsync(IEnumerable<ListCardOrder> orders, CancellationToken ct = default);
}
