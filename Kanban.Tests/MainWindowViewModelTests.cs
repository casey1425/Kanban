using CommunityToolkit.Mvvm.Messaging;
using Kanban.App.Messages;
using Kanban.App.Services;
using Kanban.App.ViewModels;
using Kanban.Tests.Support;
using Microsoft.Data.Sqlite;

namespace Kanban.Tests;

public sealed class MainWindowViewModelTests : IDisposable
{
    private readonly TestApp _app = new();

    public void Dispose() => _app.Dispose();

    private static async Task AddCardAsync(BoardListViewModel list, string title)
    {
        list.NewCardTitle = title;
        await list.AddNewCardCommand.ExecuteAsync(null);
    }

    [Fact]
    public async Task First_run_creates_default_board()
    {
        var app = await _app.StartAsync();

        Assert.Null(app.ErrorMessage);
        Assert.Equal(new[] { "To Do", "In Progress", "Done" }, app.Lists.Select(l => l.Name));
        Assert.Single(app.Lists[0].Cards); // 안내용 카드 하나
    }

    [Fact]
    public async Task Board_is_restored_after_restart()
    {
        var app = await _app.StartAsync();
        await AddCardAsync(app.Lists[0], "A");
        app.NewListName = "Review";
        await app.AddNewListCommand.ExecuteAsync(null);

        var restarted = await _app.StartAsync();

        Assert.Contains("Review", restarted.Lists.Select(l => l.Name));
        Assert.Contains("A", restarted.Lists[0].Cards.Select(c => c.Title));
    }

    [Fact]
    public async Task Add_button_is_disabled_for_blank_input()
    {
        var app = await _app.StartAsync();
        var list = app.Lists[0];

        list.NewCardTitle = "   ";
        Assert.False(list.AddNewCardCommand.CanExecute(null));

        list.NewCardTitle = "ok";
        Assert.True(list.AddNewCardCommand.CanExecute(null));
    }

    [Fact]
    public async Task Moved_card_stays_moved_after_restart()
    {
        var app = await _app.StartAsync();
        var todo = app.Lists[0];
        await AddCardAsync(todo, "A");
        await AddCardAsync(todo, "B");

        await app.Lists[1].MoveCardHereAsync(todo.Cards.First(c => c.Title == "A"), 0);
        await todo.MoveCardHereAsync(todo.Cards.First(c => c.Title == "B"), 0);

        var restarted = await _app.StartAsync();
        Assert.Equal(new[] { "B", "카드를 드래그해서 옮겨 보세요" }, restarted.Lists[0].Cards.Select(c => c.Title));
        Assert.Equal(new[] { "A" }, restarted.Lists[1].Cards.Select(c => c.Title));
    }

    [Fact]
    public async Task Deleting_a_card_or_list_is_persisted()
    {
        var app = await _app.StartAsync();
        await AddCardAsync(app.Lists[0], "A");
        await app.Lists[0].Cards.First(c => c.Title == "A").DeleteCommand.ExecuteAsync(null);
        await app.Lists[1].DeleteCommand.ExecuteAsync(null);

        var restarted = await _app.StartAsync();

        Assert.DoesNotContain("A", restarted.Lists[0].Cards.Select(c => c.Title));
        Assert.DoesNotContain("In Progress", restarted.Lists.Select(l => l.Name));
    }

    [Fact]
    public async Task Card_title_edit_is_saved_and_blank_edit_is_ignored()
    {
        var app = await _app.StartAsync();
        var card = app.Lists[0].Cards[0];
        var original = card.Title;

        card.BeginEditCommand.Execute(null);
        card.EditTitle = "   ";
        await card.CommitEditCommand.ExecuteAsync(null);
        Assert.Equal(original, card.Title);
        Assert.False(card.IsEditing);

        card.BeginEditCommand.Execute(null);
        card.EditTitle = "  새 제목  ";
        await card.CommitEditCommand.ExecuteAsync(null);

        var restarted = await _app.StartAsync();
        Assert.Equal("새 제목", restarted.Lists[0].Cards[0].Title);
    }

    [Fact]
    public async Task Save_failure_shows_error_instead_of_throwing()
    {
        var app = await _app.StartAsync();

        // DB 파일이 사라진 상황을 만든다: 연결 풀을 비우고 파일을 지우면 다음 접근은 실패한다.
        SqliteConnection.ClearAllPools();
        File.Delete(_app.DatabasePath);

        await AddCardAsync(app.Lists[0], "will fail");

        Assert.False(string.IsNullOrEmpty(app.ErrorMessage));
        app.DismissErrorCommand.Execute(null);
        Assert.Null(app.ErrorMessage);
    }

    [Fact]
    public async Task Clicking_a_card_opens_detail_and_closing_clears_it()
    {
        var app = await _app.StartAsync();
        var card = app.Lists[0].Cards[0];

        card.OpenDetailCommand.Execute(null);
        Assert.Same(card, app.Detail?.Card);

        await app.Detail!.CloseCommand.ExecuteAsync(null);
        Assert.Null(app.Detail);
    }

    [Fact]
    public void Theme_choice_is_persisted_across_restarts()
    {
        var app = _app.CreateViewModel();
        Assert.Equal(ThemeMode.System, app.ThemeMode);

        app.CycleThemeCommand.Execute(null);
        app.CycleThemeCommand.Execute(null);
        Assert.Equal(ThemeMode.Dark, app.ThemeMode);

        var restarted = _app.CreateViewModel();
        Assert.Equal(ThemeMode.Dark, restarted.ThemeMode);

        restarted.CycleThemeCommand.Execute(null); // Dark -> System
        Assert.Equal(ThemeMode.System, restarted.ThemeMode);
    }

    [Fact]
    public void Corrupt_settings_file_falls_back_to_defaults()
    {
        File.WriteAllText(_app.SettingsPath, "{ not json");

        var app = _app.CreateViewModel();

        Assert.Equal(ThemeMode.System, app.ThemeMode);
        Assert.Null(app.ErrorMessage);
    }

    [Fact]
    public void Error_messages_sent_through_the_messenger_reach_the_banner()
    {
        var app = _app.CreateViewModel();

        WeakReferenceMessenger.Default.Send(new ErrorOccurred("boom"));

        Assert.Equal("boom", app.ErrorMessage);
    }
}
