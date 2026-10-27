using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kanban.App.Undo;
using Kanban.Core;
using Kanban.Core.Models;

namespace Kanban.App.ViewModels;

public partial class ChecklistItemViewModel : ViewModelBase
{
    private readonly IBoardRepository _repository;
    private readonly CardViewModel _card;

    public int Id { get; }

    /// <summary>카드 안에서의 순서값(DB의 Position). 삭제한 항목을 복원할 때 원래 자리를 지키는 데 쓴다.</summary>
    public int Position { get; }

    public string Text { get; }

    [ObservableProperty]
    private bool _isDone;

    public ChecklistItemViewModel(ChecklistItem model, CardViewModel card, IBoardRepository repository)
    {
        _repository = repository;
        _card = card;
        Id = model.Id;
        Position = model.Position;
        Text = model.Text;
        _isDone = model.IsDone;
    }

    /// <summary>체크박스를 눌렀을 때 호출된다. IsDone은 양방향 바인딩으로 이미 바뀐 상태이다.</summary>
    [RelayCommand]
    private async Task ToggleAsync()
    {
        _card.NotifyChecklistChanged();

        var done = IsDone;
        var saved = await TryRunAsync(() => _repository.SetChecklistItemDoneAsync(Id, done), "체크 상태를 저장하지 못했어요");
        if (saved)
            Record(new ToggleChecklistItemAction(_card, this, done));
    }

    [RelayCommand]
    private Task DeleteAsync() => _card.RemoveChecklistItemAsync(this);

    /// <summary>실행 취소/다시 실행에서 체크 상태를 맞추고 저장한다(새 기록은 남기지 않는다).</summary>
    internal async Task SetDoneAsync(bool isDone)
    {
        IsDone = isDone;
        _card.NotifyChecklistChanged();
        await _repository.SetChecklistItemDoneAsync(Id, isDone);
    }

    /// <summary>삭제한 항목을 DB에 같은 Id로 다시 만들 때 쓰는 스냅샷. 실행 취소용.</summary>
    internal ChecklistItem ToSnapshot(int cardId) => new()
    {
        Id = Id,
        CardId = cardId,
        Text = Text,
        IsDone = IsDone,
        Position = Position,
    };
}
