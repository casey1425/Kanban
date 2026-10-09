using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Kanban.App.ViewModels;

public partial class BoardListViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _name;

    // 입력창과 양방향 바인딩된다. 값이 바뀌면 추가 버튼의 활성 상태도 다시 계산한다.
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddNewCardCommand))]
    private string _newCardTitle = string.Empty;

    public ObservableCollection<CardViewModel> Cards { get; } = new();

    public BoardListViewModel(string name)
    {
        _name = name;
    }

    public void AddCard(string title, string? description = null)
    {
        Cards.Add(new CardViewModel(title, description, card => Cards.Remove(card)));
    }

    private bool CanAddCard() => !string.IsNullOrWhiteSpace(NewCardTitle);

    [RelayCommand(CanExecute = nameof(CanAddCard))]
    private void AddNewCard()
    {
        AddCard(NewCardTitle.Trim());
        NewCardTitle = string.Empty;
    }
}
