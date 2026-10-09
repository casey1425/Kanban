using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Kanban.App.ViewModels;

public partial class BoardListViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _name;

    public ObservableCollection<CardViewModel> Cards { get; } = new();

    public BoardListViewModel(string name)
    {
        _name = name;
    }
}
