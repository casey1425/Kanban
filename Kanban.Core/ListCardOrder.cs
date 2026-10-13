namespace Kanban.Core;

/// <summary>한 리스트의 카드 순서. 카드를 옮긴 뒤 영향받은 리스트들의 최종 상태를 저장할 때 쓴다.</summary>
public sealed record ListCardOrder(int ListId, IReadOnlyList<int> CardIds);
