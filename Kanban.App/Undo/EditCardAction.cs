using System.Threading.Tasks;
using Kanban.App.ViewModels;

namespace Kanban.App.Undo;

/// <summary>카드의 제목, 설명, 마감일 수정.</summary>
public sealed class EditCardAction : IUndoableAction
{
    private readonly CardViewModel _card;
    private readonly int _boardId;
    private readonly CardEditState _before;
    private readonly CardEditState _after;

    public EditCardAction(CardViewModel card, int boardId, CardEditState before, CardEditState after)
    {
        _card = card;
        _boardId = boardId;
        _before = before;
        _after = after;
    }

    public string Description => "카드 수정";

    public int? UndoBoardId => _boardId;

    public int? RedoBoardId => _boardId;

    public Task UndoAsync() => _card.ApplyEditStateAsync(_before);

    public Task RedoAsync() => _card.ApplyEditStateAsync(_after);
}
