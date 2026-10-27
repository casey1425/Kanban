using Kanban.Core.Models;

namespace Kanban.Core;

/// <summary>보드 데이터의 저장소. UI는 이 인터페이스만 알고, 실제 저장 방식(SQLite 등)은 모른다.</summary>
public interface IBoardRepository
{
    /// <summary>저장소를 준비한다(DB 생성/마이그레이션). 다른 메서드보다 먼저 한 번 호출한다.</summary>
    Task InitializeAsync(CancellationToken ct = default);

    /// <summary>모든 보드를 순서대로 가져온다(리스트와 카드는 포함하지 않는다).</summary>
    Task<IReadOnlyList<Board>> GetBoardsAsync(CancellationToken ct = default);

    Task<Board> AddBoardAsync(string name, CancellationToken ct = default);

    Task RenameBoardAsync(int boardId, string name, CancellationToken ct = default);

    /// <summary>보드와 그 안의 모든 리스트, 카드를 삭제한다.</summary>
    Task DeleteBoardAsync(int boardId, CancellationToken ct = default);

    /// <summary>보드의 리스트를 순서대로, 각 리스트의 카드도 순서대로 가져온다.</summary>
    Task<IReadOnlyList<BoardList>> GetListsAsync(int boardId, CancellationToken ct = default);

    /// <summary>선택 가능한 모든 라벨(보드와 무관하게 공통).</summary>
    Task<IReadOnlyList<Label>> GetLabelsAsync(CancellationToken ct = default);

    Task<BoardList> AddListAsync(int boardId, string name, CancellationToken ct = default);

    /// <summary>
    /// 삭제했던 리스트를 카드, 라벨 연결, 체크리스트까지 포함해 <b>원래 Id 그대로</b> 다시 만든다(실행 취소용).
    /// </summary>
    Task RestoreListAsync(BoardList list, CancellationToken ct = default);

    Task DeleteListAsync(int listId, CancellationToken ct = default);

    Task<Card> AddCardAsync(int listId, string title, string? description, CancellationToken ct = default);

    /// <summary>
    /// 삭제했던 카드를 라벨 연결, 체크리스트까지 포함해 <b>원래 Id 그대로</b> 다시 만든다(실행 취소용).
    /// card.ListId의 리스트가 존재해야 한다.
    /// </summary>
    Task RestoreCardAsync(Card card, CancellationToken ct = default);

    Task UpdateCardAsync(Card card, CancellationToken ct = default);

    /// <summary>카드에 붙은 라벨을 주어진 목록으로 교체한다.</summary>
    Task SetCardLabelsAsync(int cardId, IReadOnlyCollection<int> labelIds, CancellationToken ct = default);

    Task<ChecklistItem> AddChecklistItemAsync(int cardId, string text, CancellationToken ct = default);

    /// <summary>삭제했던 체크리스트 항목을 <b>원래 Id 그대로</b> 다시 만든다(실행 취소용).</summary>
    Task RestoreChecklistItemAsync(ChecklistItem item, CancellationToken ct = default);

    Task SetChecklistItemDoneAsync(int itemId, bool isDone, CancellationToken ct = default);

    Task DeleteChecklistItemAsync(int itemId, CancellationToken ct = default);

    Task DeleteCardAsync(int cardId, CancellationToken ct = default);

    /// <summary>주어진 리스트들에 대해 카드의 소속 리스트와 순서를 한 번에 저장한다.</summary>
    Task SaveCardOrderAsync(IEnumerable<ListCardOrder> orders, CancellationToken ct = default);
}
