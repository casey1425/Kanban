using CommunityToolkit.Mvvm.ComponentModel;

namespace Kanban.App.ViewModels;

public partial class CardViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _title;

    [ObservableProperty]
    private string? _description;

    public CardViewModel(string title, string? description = null)
    {
        _title = title;
        _description = description;
    }
}
