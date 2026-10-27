using System.Threading.Tasks;
using Kanban.App.ViewModels;

namespace Kanban.App.Undo;

/// <summary>
/// 체크리스트 항목 추가 또는 삭제. 카드·리스트와 같은 방식으로, 서로 반대 동작이라 하나로 표현한다.
/// 삭제한 항목은 같은 Id, 체크 상태, 자리 그대로 복원된다.
/// </summary>
public sealed class ChecklistItemPresenceAction : IUndoableAction
{
    private readonly CardViewModel _card;
    private readonly ChecklistItemViewModel _item;
    private readonly int _index;
    private readonly bool _wasAdded;

    /// <param name="wasAdded">true면 "항목을 추가한" 작업, false면 "항목을 삭제한" 작업.</param>
    public ChecklistItemPresenceAction(CardViewModel card, ChecklistItemViewModel item, int index, bool wasAdded)
    {
        _card = card;
        _item = item;
        _index = index;
        _wasAdded = wasAdded;
    }

    public string Description => _wasAdded ? "체크리스트 항목 추가" : "체크리스트 항목 삭제";

    public CardViewModel? DetailScope => _card;

    public int? UndoBoardId => _card.Owner?.BoardId;

    public int? RedoBoardId => _card.Owner?.BoardId;

    public Task UndoAsync() => _wasAdded ? Remove() : Restore();

    public Task RedoAsync() => _wasAdded ? Restore() : Remove();

    private Task Remove() => _card.RemoveChecklistItemWithoutHistoryAsync(_item);

    private Task Restore() => _card.RestoreChecklistItemAsync(_item, _index);
}
