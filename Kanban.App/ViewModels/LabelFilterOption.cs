namespace Kanban.App.ViewModels;

/// <summary>라벨 필터 드롭다운의 항목. LabelId가 null이면 "모든 라벨"(필터 없음)이다.</summary>
public sealed record LabelFilterOption(int? LabelId, string Name);
