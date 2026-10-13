namespace Kanban.Core.Models;

public class Card
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>소속 리스트 안에서의 순서(0부터).</summary>
    public int Position { get; set; }

    /// <summary>마감일. 시각 없이 날짜만 다룬다.</summary>
    public DateOnly? DueDate { get; set; }

    public int ListId { get; set; }

    public BoardList? List { get; set; }
}
