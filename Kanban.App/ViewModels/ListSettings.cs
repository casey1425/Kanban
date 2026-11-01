namespace Kanban.App.ViewModels;

/// <summary>리스트의 편집 가능한 설정 한 벌. 설정 변경 기록의 "전/후" 상태로 쓴다.</summary>
/// <param name="WipLimit">동시 진행 제한. null이면 제한 없음.</param>
public sealed record ListSettings(string Name, bool IsDoneList, int? WipLimit);
