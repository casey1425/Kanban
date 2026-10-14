namespace Kanban.App.Messages;

/// <summary>사용자에게 알려야 할 오류. ViewModel은 창을 모른 채 이 메시지만 보낸다.</summary>
public sealed record ErrorOccurred(string Message);
