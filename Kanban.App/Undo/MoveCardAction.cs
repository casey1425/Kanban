using System.Threading.Tasks;
using Kanban.App.ViewModels;

namespace Kanban.App.Undo;

/// <summary>카드 이동: 같은 리스트 안, 리스트 사이, 보드 사이 모두 이 하나로 표현한다.</summary>
public sealed class MoveCardAction : IUndoableAction
{
    private readonly CardViewModel _card;
    private readonly BoardListViewModel _fromList;
    private readonly int _fromIndex;
    private readonly BoardListViewModel _toList;
    private readonly int _toIndex;

    public MoveCardAction(
        CardViewModel card,
        BoardListViewModel fromList, int fromIndex,
        BoardListViewModel toList, int toIndex)
    {
        _card = card;
        _fromList = fromList;
        _fromIndex = fromIndex;
        _toList = toList;
        _toIndex = toIndex;
    }

    public CardViewModel? DetailScope => null;

    public string Description => "카드 이동";

    public int? UndoBoardId => _fromList.BoardId;

    public int? RedoBoardId => _toList.BoardId;

    public Task UndoAsync() => _fromList.PlaceCardAsync(_card, _fromIndex);

    public Task RedoAsync() => _toList.PlaceCardAsync(_card, _toIndex);
}
