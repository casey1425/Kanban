using System;
using System.Collections.Generic;

namespace Kanban.App.Undo;

/// <summary>실행 취소 / 다시 실행 기록. 메모리에만 있고 앱을 끄면 사라진다.</summary>
public sealed class UndoHistory
{
    public const int MaxSize = 100;

    private readonly List<IUndoableAction> _undo = new();
    private readonly List<IUndoableAction> _redo = new();

    /// <summary>기록이 바뀔 때마다 발생한다(버튼 활성 상태와 툴팁을 갱신하는 데 쓴다).</summary>
    public event EventHandler? Changed;

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    /// <summary>다음에 실행 취소될 작업(가장 최근 작업).</summary>
    public IUndoableAction? NextUndo => _undo.Count > 0 ? _undo[^1] : null;

    /// <summary>다음에 다시 실행될 작업(가장 최근에 취소한 작업).</summary>
    public IUndoableAction? NextRedo => _redo.Count > 0 ? _redo[^1] : null;

    /// <summary>새 작업을 기록한다. 새 작업이 생기면 "다시 실행" 기록은 의미가 없어지므로 비운다.</summary>
    public void Push(IUndoableAction action)
    {
        _undo.Add(action);
        if (_undo.Count > MaxSize)
            _undo.RemoveAt(0);

        _redo.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>NextUndo를 되돌리는 데 성공했을 때 호출한다. 그 작업은 "다시 실행" 쪽으로 옮겨진다.</summary>
    public void CompleteUndo()
    {
        if (_undo.Count == 0)
            return;

        _redo.Add(_undo[^1]);
        _undo.RemoveAt(_undo.Count - 1);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>NextRedo를 다시 실행하는 데 성공했을 때 호출한다. 그 작업은 "실행 취소" 쪽으로 돌아온다.</summary>
    public void CompleteRedo()
    {
        if (_redo.Count == 0)
            return;

        _undo.Add(_redo[^1]);
        _redo.RemoveAt(_redo.Count - 1);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        if (_undo.Count == 0 && _redo.Count == 0)
            return;

        _undo.Clear();
        _redo.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
