using Kanban.App.ViewModels;

namespace Kanban.App.Messages;

/// <summary>카드 상세를 열어 달라는 요청. 카드는 창(MainWindow)을 직접 모른 채 이 메시지만 보낸다.</summary>
public sealed record OpenCardRequested(CardViewModel Card);
