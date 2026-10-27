using System;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;

namespace Kanban.App.ViewModels;

/// <summary>카드 상세 패널. 편집 대상 카드를 감싸고, 닫을 때 변경 내용을 저장한다.</summary>
public partial class CardDetailViewModel : ViewModelBase
{
    private readonly Action _onClosed;

    public CardViewModel Card { get; }

    /// <summary>
    /// 패널을 덮는 배경 때문에 사이드바의 실행 취소 버튼을 누를 수 없으므로, 같은 명령을 패널 안에서도 쓸 수 있게 받아 둔다.
    /// </summary>
    public ICommand UndoCommand { get; }

    public ICommand RedoCommand { get; }

    public CardDetailViewModel(CardViewModel card, Action onClosed, ICommand undoCommand, ICommand redoCommand)
    {
        Card = card;
        _onClosed = onClosed;
        UndoCommand = undoCommand;
        RedoCommand = redoCommand;
    }

    [RelayCommand]
    private async Task CloseAsync()
    {
        await Card.SaveAsync();
        _onClosed();
    }

    [RelayCommand]
    private void ClearDueDate() => Card.DueDate = null;
}
