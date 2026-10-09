using System.Collections.ObjectModel;

namespace Kanban.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public string BoardTitle { get; } = "My Kanban Board";

    public ObservableCollection<BoardListViewModel> Lists { get; } = new();

    public MainWindowViewModel()
    {
        // 1단계: 하드코딩된 샘플 데이터. 이후 단계에서 DB 로딩으로 대체한다.
        var todo = new BoardListViewModel("To Do");
        todo.AddCard("Avalonia 공부하기", "XAML, 바인딩, 스타일");
        todo.AddCard("README 작성");
        todo.AddCard("드래그 앤 드롭 조사");

        var doing = new BoardListViewModel("In Progress");
        doing.AddCard("보드 화면 레이아웃", "ItemsControl + DataTemplate");

        var done = new BoardListViewModel("Done");
        done.AddCard("Rider 설치");
        done.AddCard("프로젝트 생성");

        Lists.Add(todo);
        Lists.Add(doing);
        Lists.Add(done);
    }
}
