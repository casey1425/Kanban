using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kanban.Core;
using Kanban.Core.Models;

namespace Kanban.App.ViewModels;

public partial class CardViewModel : ViewModelBase
{
    private readonly IBoardRepository _repository;

    public int Id { get; }

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

    public CardViewModel(Card model, IBoardRepository repository)
    {
        _repository = repository;
        Id = model.Id;
        _title = model.Title;
        _description = model.Description;
    }

    private Card ToModel() => new()
    {
        Id = Id,
        Title = Title,
        Description = Description,
    };

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (Owner is not null)
            await Owner.RemoveCardAsync(this);
    }

    [RelayCommand]
    private void BeginEdit()
    {
        EditTitle = Title;
        IsEditing = true;
    }

    [RelayCommand]
    private async Task CommitEditAsync()
    {
        IsEditing = false;
        if (string.IsNullOrWhiteSpace(EditTitle))
            return;

        Title = EditTitle.Trim();
        await _repository.UpdateCardAsync(ToModel());
    }

    [RelayCommand]
    private void CancelEdit() => IsEditing = false;
}
