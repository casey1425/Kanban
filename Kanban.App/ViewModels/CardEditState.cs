using System;

namespace Kanban.App.ViewModels;

/// <summary>카드의 편집 가능한 값(제목, 설명, 마감일) 한 벌. 수정 기록의 "전/후" 상태로 쓴다.</summary>
public sealed record CardEditState(string Title, string? Description, DateOnly? DueDate);
