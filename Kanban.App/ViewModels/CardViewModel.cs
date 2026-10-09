using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Kanban.App.ViewModels;

public partial class CardViewModel : ViewModelBase
{
    private readonly Action<CardViewModel> _onDelete;

    [ObservableProperty]
    private string _title;

    [ObservableProperty]
    private string? _description;

    // 카드는 자신이 속한 리스트를 모른다. 삭제는 리스트가 넘겨준 콜백에 맡긴다.
    public CardViewModel(string title, string? description, Action<CardViewModel> onDelete)
    {
        _title = title;
        _description = description;
        _onDelete = onDelete;
    }

    [RelayCommand]
    private void Delete() => _onDelete(this);
}
