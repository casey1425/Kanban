namespace Kanban.Core.Models;

public class BoardList
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>보드 안에서의 순서(0부터).</summary>
    public int Position { get; set; }

    public List<Card> Cards { get; set; } = new();
}
