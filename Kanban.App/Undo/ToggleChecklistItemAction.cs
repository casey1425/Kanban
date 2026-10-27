using System.Threading.Tasks;
using Kanban.App.ViewModels;

namespace Kanban.App.Undo;

/// <summary>체크리스트 항목의 체크를 켜거나 끈 작업.</summary>
public sealed class ToggleChecklistItemAction : IUndoableAction
{
    private readonly CardViewModel _card;
    private readonly ChecklistItemViewModel _item;
    private readonly bool _doneAfter;

    /// <param name="doneAfter">작업 후에 항목이 체크돼 있는지(true) 해제됐는지(false).</param>
    public ToggleChecklistItemAction(CardViewModel card, ChecklistItemViewModel item, bool doneAfter)
    {
        _card = card;
        _item = item;
        _doneAfter = doneAfter;
    }

    public string Description => "체크 상태 변경";

    public CardViewModel? DetailScope => _card;

    public int? UndoBoardId => _card.Owner?.BoardId;

    public int? RedoBoardId => _card.Owner?.BoardId;

    public Task UndoAsync() => _item.SetDoneAsync(!_doneAfter);

    public Task RedoAsync() => _item.SetDoneAsync(_doneAfter);
}
