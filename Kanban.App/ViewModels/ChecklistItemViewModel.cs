using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kanban.Core;
using Kanban.Core.Models;

namespace Kanban.App.ViewModels;

public partial class ChecklistItemViewModel : ViewModelBase
{
    private readonly IBoardRepository _repository;
    private readonly Func<ChecklistItemViewModel, Task> _onDelete;
    private readonly Action _onChanged;

    public int Id { get; }

    public string Text { get; }

    [ObservableProperty]
    private bool _isDone;

    public ChecklistItemViewModel(
        ChecklistItem model,
        IBoardRepository repository,
        Action onChanged,
        Func<ChecklistItemViewModel, Task> onDelete)
    {
        _repository = repository;
        _onChanged = onChanged;
        _onDelete = onDelete;
        Id = model.Id;
        Text = model.Text;
        _isDone = model.IsDone;
    }

    /// <summary>체크박스를 눌렀을 때 호출된다. IsDone은 양방향 바인딩으로 이미 바뀐 상태이다.</summary>
    [RelayCommand]
    private Task ToggleAsync()
    {
        _onChanged();
        return RunSafeAsync(() => _repository.SetChecklistItemDoneAsync(Id, IsDone), "체크 상태를 저장하지 못했어요");
    }

    [RelayCommand]
    private Task DeleteAsync() => _onDelete(this);
}
