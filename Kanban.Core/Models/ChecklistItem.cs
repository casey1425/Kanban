namespace Kanban.Core.Models;

public class ChecklistItem
{
    public int Id { get; set; }

    public string Text { get; set; } = string.Empty;

    public bool IsDone { get; set; }

    /// <summary>카드 안에서의 순서(0부터).</summary>
    public int Position { get; set; }

    public int CardId { get; set; }

    public Card? Card { get; set; }
}
