using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Kanban.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public string BoardTitle { get; } = "My Kanban Board";

    public ObservableCollection<BoardListViewModel> Lists { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddNewListCommand))]
    private string _newListName = string.Empty;

    public MainWindowViewModel()
    {
        // 하드코딩된 샘플 데이터. 이후 단계에서 DB 로딩으로 대체한다.
        var todo = AddList("To Do");
        todo.AddCard("Avalonia 공부하기", "XAML, 바인딩, 스타일");
        todo.AddCard("README 작성");
        todo.AddCard("드래그 앤 드롭 조사");

        var doing = AddList("In Progress");
        doing.AddCard("보드 화면 레이아웃", "ItemsControl + DataTemplate");

        var done = AddList("Done");
        done.AddCard("Rider 설치");
        done.AddCard("프로젝트 생성");
    }

    private BoardListViewModel AddList(string name)
    {
        var list = new BoardListViewModel(name, l => Lists.Remove(l));
        Lists.Add(list);
        return list;
    }

    private bool CanAddNewList() => !string.IsNullOrWhiteSpace(NewListName);

    [RelayCommand(CanExecute = nameof(CanAddNewList))]
    private void AddNewList()
    {
        AddList(NewListName.Trim());
        NewListName = string.Empty;
    }
}
