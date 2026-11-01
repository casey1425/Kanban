using System.Threading.Tasks;
using Kanban.App.ViewModels;

namespace Kanban.App.Undo;

/// <summary>리스트의 이름, 완료 리스트 여부, WIP 제한 변경.</summary>
public sealed class EditListSettingsAction : IUndoableAction
{
    private readonly BoardListViewModel _list;
    private readonly ListSettings _before;
    private readonly ListSettings _after;

    public EditListSettingsAction(BoardListViewModel list, ListSettings before, ListSettings after)
    {
        _list = list;
        _before = before;
        _after = after;
    }

    public string Description => "리스트 설정 변경";

    public CardViewModel? DetailScope => null;

    public int? UndoBoardId => _list.BoardId;

    public int? RedoBoardId => _list.BoardId;

    public Task UndoAsync() => _list.ApplySettingsAsync(_before);

    public Task RedoAsync() => _list.ApplySettingsAsync(_after);
}
