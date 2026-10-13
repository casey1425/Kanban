using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Kanban.App.Messages;
using Kanban.Core;
using Kanban.Core.Models;

namespace Kanban.App.ViewModels;

public partial class CardViewModel : ViewModelBase
{
    private readonly IBoardRepository _repository;
    private string _savedTitle;

    public int Id { get; }

    [ObservableProperty]
    private string _title;

    [ObservableProperty]
    private string? _description;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDueDate))]
    [NotifyPropertyChangedFor(nameof(DueDateText))]
    [NotifyPropertyChangedFor(nameof(IsOverdue))]
    [NotifyPropertyChangedFor(nameof(DueDateTime))]
    private DateOnly? _dueDate;

    [ObservableProperty]
    private bool _isEditing;

    // 편집 중인 임시 값. 확정(Enter) 전에는 Title을 건드리지 않는다.
    [ObservableProperty]
    private string _editTitle = string.Empty;

    /// <summary>카드가 속한 리스트. 리스트의 Insert/Remove가 관리한다.</summary>
    public BoardListViewModel? Owner { get; internal set; }

    public bool HasDueDate => DueDate is not null;

    public string DueDateText => DueDate is { } d ? d.ToString("M/d") : string.Empty;

    public bool IsOverdue => DueDate is { } d && d < DateOnly.FromDateTime(DateTime.Today);

    /// <summary>날짜 선택 컨트롤(DateTimeOffset?)과 연결하기 위한 변환용 속성.</summary>
    public DateTimeOffset? DueDateTime
    {
        get => DueDate is { } d ? new DateTimeOffset(d.ToDateTime(TimeOnly.MinValue)) : null;
        set => DueDate = value is { } v ? DateOnly.FromDateTime(v.DateTime) : null;
    }

    public CardViewModel(Card model, IBoardRepository repository)
    {
        _repository = repository;
        Id = model.Id;
        _title = model.Title;
        _savedTitle = model.Title;
        _description = model.Description;
        _dueDate = model.DueDate;
    }

    private Card ToModel() => new()
    {
        Id = Id,
        Title = Title,
        Description = string.IsNullOrWhiteSpace(Description) ? null : Description,
        DueDate = DueDate,
    };

    /// <summary>제목, 설명, 마감일을 저장한다. 제목을 비워 둔 경우 마지막으로 저장된 제목으로 되돌린다.</summary>
    public async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Title))
            Title = _savedTitle;

        await _repository.UpdateCardAsync(ToModel());
        _savedTitle = Title;
    }

    [RelayCommand]
    private void OpenDetail() => WeakReferenceMessenger.Default.Send(new OpenCardRequested(this));

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
        await SaveAsync();
    }

    [RelayCommand]
    private void CancelEdit() => IsEditing = false;
}
