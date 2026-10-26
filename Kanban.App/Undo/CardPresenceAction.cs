using System.Threading.Tasks;
using Kanban.App.ViewModels;

namespace Kanban.App.Undo;

/// <summary>
/// 카드 추가 또는 삭제. 둘은 서로 반대 동작이라 하나로 표현한다
/// (추가의 취소 = 삭제, 삭제의 취소 = 같은 Id로 다시 만들기).
/// </summary>
public sealed class CardPresenceAction : IUndoableAction
{
    private readonly CardViewModel _card;
    private readonly BoardListViewModel _list;
    private readonly int _index;
    private readonly bool _wasAdded;

    /// <param name="wasAdded">true면 "카드를 추가한" 작업, false면 "카드를 삭제한" 작업.</param>
    public CardPresenceAction(CardViewModel card, BoardListViewModel list, int index, bool wasAdded)
    {
        _card = card;
        _list = list;
        _index = index;
        _wasAdded = wasAdded;
    }

    public string Description => _wasAdded ? "카드 추가" : "카드 삭제";

    public int? UndoBoardId => _list.BoardId;

    public int? RedoBoardId => _list.BoardId;

    public Task UndoAsync() => _wasAdded ? Remove() : Restore();

    public Task RedoAsync() => _wasAdded ? Restore() : Remove();

    private Task Remove() => _list.RemoveCardWithoutHistoryAsync(_card);

    private Task Restore() => _list.RestoreCardAsync(_card, _index);
}
