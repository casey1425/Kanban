using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Kanban.App.Messages;
using Kanban.Core;
using Kanban.Core.Models;

namespace Kanban.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IBoardRepository _repository;
    private IReadOnlyList<Label> _labels = Array.Empty<Label>();

    public string BoardTitle { get; } = "My Kanban Board";

    public ObservableCollection<BoardListViewModel> Lists { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddNewListCommand))]
    private string _newListName = string.Empty;

    /// <summary>데이터를 불러오지 못했을 때 화면 상단에 보여 줄 메시지.</summary>
    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>열려 있는 카드 상세 패널. null이면 닫혀 있다.</summary>
    [ObservableProperty]
    private CardDetailViewModel? _detail;

    public MainWindowViewModel(IBoardRepository repository)
    {
        _repository = repository;

        WeakReferenceMessenger.Default.Register<MainWindowViewModel, OpenCardRequested>(
            this, (vm, message) => vm.Detail = new CardDetailViewModel(message.Card, () => vm.Detail = null));
    }

    [RelayCommand]
    private void CloseDetail() => Detail?.CloseCommand.Execute(null);

    /// <summary>DB를 준비하고 저장된 보드를 불러온다. 처음 실행이면 기본 리스트를 만든다.</summary>
    public async Task InitializeAsync()
    {
        try
        {
            await _repository.InitializeAsync();

            _labels = await _repository.GetLabelsAsync();

            var lists = await _repository.GetListsAsync();
            if (lists.Count == 0)
                lists = await SeedDefaultBoardAsync();

            foreach (var list in lists)
                Lists.Add(CreateListViewModel(list));
        }
        catch (Exception ex)
        {
            ErrorMessage = $"데이터를 불러오지 못했어요: {ex.Message}";
        }
    }

    private async Task<IReadOnlyList<BoardList>> SeedDefaultBoardAsync()
    {
        var todo = await _repository.AddListAsync("To Do");
        await _repository.AddListAsync("In Progress");
        await _repository.AddListAsync("Done");
        await _repository.AddCardAsync(todo.Id, "카드를 드래그해서 옮겨 보세요", "✎로 제목을 수정하고 ✕로 삭제할 수 있어요");

        return await _repository.GetListsAsync();
    }

    private BoardListViewModel CreateListViewModel(BoardList model) =>
        new(model, _repository, _labels, list => Lists.Remove(list));

    private bool CanAddNewList() => !string.IsNullOrWhiteSpace(NewListName);

    [RelayCommand(CanExecute = nameof(CanAddNewList))]
    private async Task AddNewListAsync()
    {
        var name = NewListName.Trim();
        NewListName = string.Empty;

        var model = await _repository.AddListAsync(name);
        Lists.Add(CreateListViewModel(model));
    }
}
