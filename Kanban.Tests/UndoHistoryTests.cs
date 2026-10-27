using Kanban.App.Undo;
using Kanban.App.ViewModels;

namespace Kanban.Tests;

public sealed class UndoHistoryTests
{
    private sealed class FakeAction : IUndoableAction
    {
        public FakeAction(string description) => Description = description;

        public string Description { get; }
        public CardViewModel? DetailScope => null;
        public int? UndoBoardId => null;
        public int? RedoBoardId => null;
        public Task UndoAsync() => Task.CompletedTask;
        public Task RedoAsync() => Task.CompletedTask;
    }

    [Fact]
    public void Empty_history_has_nothing_to_undo_or_redo()
    {
        var history = new UndoHistory();

        Assert.False(history.CanUndo);
        Assert.False(history.CanRedo);
        Assert.Null(history.NextUndo);
        Assert.Null(history.NextRedo);
    }

    [Fact]
    public void Undo_and_redo_move_the_latest_action_between_the_stacks()
    {
        var history = new UndoHistory();
        history.Push(new FakeAction("one"));
        history.Push(new FakeAction("two"));
        Assert.Equal("two", history.NextUndo!.Description);

        history.CompleteUndo();
        Assert.Equal("one", history.NextUndo!.Description);
        Assert.Equal("two", history.NextRedo!.Description);

        history.CompleteRedo();
        Assert.Equal("two", history.NextUndo!.Description);
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void Pushing_a_new_action_discards_redo_history()
    {
        var history = new UndoHistory();
        history.Push(new FakeAction("one"));
        history.CompleteUndo();
        Assert.True(history.CanRedo);

        history.Push(new FakeAction("two"));

        Assert.False(history.CanRedo);
        Assert.Equal("two", history.NextUndo!.Description);
    }

    [Fact]
    public void History_keeps_only_the_most_recent_actions()
    {
        var history = new UndoHistory();
        for (var i = 0; i < UndoHistory.MaxSize + 5; i++)
            history.Push(new FakeAction($"a{i}"));

        var undone = 0;
        while (history.CanUndo)
        {
            history.CompleteUndo();
            undone++;
        }

        Assert.Equal(UndoHistory.MaxSize, undone);
        Assert.Equal("a5", history.NextRedo!.Description);   // 가장 오래된 5개는 버려졌다
    }

    [Fact]
    public void Changed_is_raised_only_when_something_changes()
    {
        var history = new UndoHistory();
        var raised = 0;
        history.Changed += (_, _) => raised++;

        history.Clear();                       // 비어 있으므로 변화 없음
        history.CompleteUndo();                // 되돌릴 게 없으므로 변화 없음
        Assert.Equal(0, raised);

        history.Push(new FakeAction("one"));
        history.CompleteUndo();
        history.CompleteRedo();
        history.Clear();
        Assert.Equal(4, raised);
    }
}
