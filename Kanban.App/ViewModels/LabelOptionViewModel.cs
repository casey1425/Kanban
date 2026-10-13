using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kanban.Core.Models;

namespace Kanban.App.ViewModels;

/// <summary>상세 패널에서 라벨을 켜고 끄는 칩. 카드마다 라벨 수만큼 하나씩 가진다.</summary>
public partial class LabelOptionViewModel : ViewModelBase
{
    private readonly Func<Task> _onToggled;

    public int Id { get; }
    public string Name { get; }
    public string Color { get; }

    [ObservableProperty]
    private bool _isSelected;

    public LabelOptionViewModel(Label model, bool isSelected, Func<Task> onToggled)
    {
        Id = model.Id;
        Name = model.Name;
        Color = model.Color;
        _isSelected = isSelected;
        _onToggled = onToggled;
    }

    [RelayCommand]
    private async Task ToggleAsync()
    {
        IsSelected = !IsSelected;
        await _onToggled();
    }
}
