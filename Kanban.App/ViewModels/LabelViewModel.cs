using Kanban.Core.Models;

namespace Kanban.App.ViewModels;

/// <summary>카드에 붙은 라벨 하나를 화면에 보여 주기 위한 읽기 전용 모델.</summary>
public sealed class LabelViewModel
{
    public int Id { get; }
    public string Name { get; }
    public string Color { get; }

    public LabelViewModel(Label model)
    {
        Id = model.Id;
        Name = model.Name;
        Color = model.Color;
    }
}
