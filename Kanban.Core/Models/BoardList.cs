namespace Kanban.Core.Models;

public class BoardList
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>보드 안에서의 순서(0부터).</summary>
    public int Position { get; set; }

    /// <summary>
    /// 완료 리스트인지. 이 리스트에 있는 카드는 "완료"로 집계한다(통계에서 쓴다).
    /// </summary>
    public bool IsDoneList { get; set; }

    /// <summary>동시 진행 제한(WIP limit). 카드 수가 이 값을 넘으면 경고한다. null이면 제한 없음.</summary>
    public int? WipLimit { get; set; }

    public int BoardId { get; set; }

    public Board? Board { get; set; }

    public List<Card> Cards { get; set; } = new();
}
