namespace Kanban.Core.Models;

public class Label
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>표시 색상. "#RRGGBB" 형식.</summary>
    public string Color { get; set; } = "#808080";

    public List<Card> Cards { get; set; } = new();
}
