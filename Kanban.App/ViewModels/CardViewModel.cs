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

    [ObservableProperty]
    private bool _isEditing;

    // 편집 중인 임시 값. 확정(Enter) 전에는 Title을 건드리지 않는다.
    [ObservableProperty]
    private string _editTitle = string.Empty;

    // 카드는 자신이 속한 리스트를 모른다. 삭제는 리스트가 넘겨준 콜백에 맡긴다.
    public CardViewModel(string title, string? description, Action<CardViewModel> onDelete)
    {
        _title = title;
        _description = description;
        _onDelete = onDelete;
    }

    [RelayCommand]
    private void Delete() => _onDelete(this);

    [RelayCommand]
    private void BeginEdit()
    {
        EditTitle = Title;
        IsEditing = true;
    }

    [RelayCommand]
    private void CommitEdit()
    {
        if (!string.IsNullOrWhiteSpace(EditTitle))
            Title = EditTitle.Trim();
        IsEditing = false;
    }

    [RelayCommand]
    private void CancelEdit() => IsEditing = false;
}
