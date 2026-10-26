using Kanban.App.Undo;

namespace Kanban.App.Messages;

/// <summary>되돌릴 수 있는 작업이 방금 실행(저장까지 성공)됐다는 알림. MainWindowViewModel이 기록에 쌓는다.</summary>
public sealed record ActionPerformed(IUndoableAction Action);
