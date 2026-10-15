namespace Kanban.Core.Models;

public class Board
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>보드 목록에서의 순서(0부터).</summary>
    public int Position { get; set; }

    public List<BoardList> Lists { get; set; } = new();
}
