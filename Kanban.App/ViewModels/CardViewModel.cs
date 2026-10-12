using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Kanban.App.ViewModels;

public partial class CardViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _title;

    [ObservableProperty]
    private string? _description;

    [ObservableProperty]
    private bool _isEditing;

    // 편집 중인 임시 값. 확정(Enter) 전에는 Title을 건드리지 않는다.
    [ObservableProperty]
    private string _editTitle = string.Empty;

    /// <summary>카드가 속한 리스트. 리스트의 Insert/Remove가 관리한다.</summary>
    public BoardListViewModel? Owner { get; internal set; }

    public CardViewModel(string title, string? description = null)
    {
        _title = title;
        _description = description;
    }

    [RelayCommand]
    private void Delete() => Owner?.RemoveCard(this);

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
