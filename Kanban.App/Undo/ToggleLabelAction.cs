using System.Threading.Tasks;
using Kanban.App.ViewModels;

namespace Kanban.App.Undo;

/// <summary>카드에 라벨 하나를 붙이거나 뗀 작업.</summary>
public sealed class ToggleLabelAction : IUndoableAction
{
    private readonly CardViewModel _card;
    private readonly int _labelId;
    private readonly bool _selectedAfter;

    /// <param name="selectedAfter">작업 후에 라벨이 붙어 있는지(true) 떼어졌는지(false).</param>
    public ToggleLabelAction(CardViewModel card, int labelId, bool selectedAfter)
    {
        _card = card;
        _labelId = labelId;
        _selectedAfter = selectedAfter;
    }

    public string Description => "라벨 변경";

    public CardViewModel? DetailScope => _card;

    public int? UndoBoardId => _card.Owner?.BoardId;

    public int? RedoBoardId => _card.Owner?.BoardId;

    public Task UndoAsync() => _card.SetLabelSelectedAsync(_labelId, !_selectedAfter);

    public Task RedoAsync() => _card.SetLabelSelectedAsync(_labelId, _selectedAfter);
}
