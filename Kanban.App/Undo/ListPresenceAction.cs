using System.Threading.Tasks;
using Kanban.App.ViewModels;

namespace Kanban.App.Undo;

/// <summary>
/// 리스트 추가 또는 삭제. 카드 추가/삭제와 같은 방식으로, 서로 반대 동작이라 하나로 표현한다.
/// 삭제한 리스트를 되돌리면 그 안의 카드, 라벨, 체크리스트도 함께 돌아온다.
/// </summary>
public sealed class ListPresenceAction : IUndoableAction
{
    private readonly BoardViewModel _board;
    private readonly BoardListViewModel _list;
    private readonly int _index;
    private readonly bool _wasAdded;

    /// <param name="wasAdded">true면 "리스트를 추가한" 작업, false면 "리스트를 삭제한" 작업.</param>
    public ListPresenceAction(BoardViewModel board, BoardListViewModel list, int index, bool wasAdded)
    {
        _board = board;
        _list = list;
        _index = index;
        _wasAdded = wasAdded;
    }

    public CardViewModel? DetailScope => null;

    public string Description => _wasAdded ? "리스트 추가" : "리스트 삭제";

    public int? UndoBoardId => _board.Id;

    public int? RedoBoardId => _board.Id;

    public Task UndoAsync() => _wasAdded ? Remove() : Restore();

    public Task RedoAsync() => _wasAdded ? Restore() : Remove();

    private Task Remove() => _board.RemoveListWithoutHistoryAsync(_list);

    private Task Restore() => _board.RestoreListAsync(_list, _index);
}
