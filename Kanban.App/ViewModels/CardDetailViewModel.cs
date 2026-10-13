using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;

namespace Kanban.App.ViewModels;

/// <summary>카드 상세 패널. 편집 대상 카드를 감싸고, 닫을 때 변경 내용을 저장한다.</summary>
public partial class CardDetailViewModel : ViewModelBase
{
    private readonly Action _onClosed;

    public CardViewModel Card { get; }

    public CardDetailViewModel(CardViewModel card, Action onClosed)
    {
        Card = card;
        _onClosed = onClosed;
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
