using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Kanban.App.ViewModels;
using Kanban.App.Views;
using Kanban.Tests.Support;

namespace Kanban.Tests;

/// <summary>
/// XAML 바인딩과 마크업이 실제로 그려지는지 확인한다.
/// ViewModel 테스트만으로는 "화면에 안 나오는" 문제(마크업 누락, 바인딩 타입 불일치)를 잡을 수 없다.
/// </summary>
public sealed class UiSmokeTests : IDisposable
{
    private readonly TestApp _app = new();

    public void Dispose() => _app.Dispose();

    private async Task<(MainWindow Window, MainWindowViewModel ViewModel)> ShowWindowAsync()
    {
        var viewModel = await _app.StartAsync();
        var window = new MainWindow { DataContext = viewModel };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, viewModel);
    }

    [AvaloniaFact]
    public async Task Cards_show_their_labels()
    {
        var (window, viewModel) = await ShowWindowAsync();
        var card = viewModel.SelectedBoard!.Lists[0].Cards[0];

        await card.LabelOptions[0].ToggleCommand.ExecuteAsync(null); // 긴급
        await card.LabelOptions[2].ToggleCommand.ExecuteAsync(null); // 기능
        Dispatcher.UIThread.RunJobs();

        var cardView = window.GetVisualDescendants().OfType<CardView>().First(v => v.DataContext == card);
        var chipTexts = cardView.GetVisualDescendants()
            .OfType<TextBlock>()
            .Where(t => t.DataContext is LabelViewModel)
            .Select(t => t.Text)
            .ToList();

        Assert.Equal(new[] { "긴급", "기능" }, chipTexts);
    }

    [AvaloniaFact]
    public async Task Detail_panel_binds_due_date_without_errors()
    {
        var (window, viewModel) = await ShowWindowAsync();
        var card = viewModel.SelectedBoard!.Lists[0].Cards[0];
        card.DueDate = new DateOnly(2030, 3, 9);

        card.OpenDetailCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        var picker = window.GetVisualDescendants().OfType<CalendarDatePicker>().Single();
        Assert.Equal(new DateTime(2030, 3, 9), picker.SelectedDate);
        Assert.False(Avalonia.Controls.DataValidationErrors.GetHasErrors(picker));
    }

    [AvaloniaFact]
    public async Task Detail_panel_has_a_visible_close_button()
    {
        var (window, viewModel) = await ShowWindowAsync();

        viewModel.SelectedBoard!.Lists[0].Cards[0].OpenDetailCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        // 오류 배너에도 "닫기"가 있지만, 오류가 없으면 숨겨져 있다. 화면에 보이는 것은 상세 패널의 것 하나여야 한다.
        var visibleClose = window.GetVisualDescendants()
            .OfType<Button>()
            .Where(b => Equals(b.Content, "닫기") && b.IsEffectivelyVisible)
            .ToList();

        var close = Assert.Single(visibleClose);
        Assert.True(close.Bounds.Height > 0);
    }

    [AvaloniaFact]
    public async Task Filtered_out_cards_are_hidden_from_the_board()
    {
        var (window, viewModel) = await ShowWindowAsync();
        var todo = viewModel.SelectedBoard!.Lists[0];
        todo.NewCardTitle = "second";
        await todo.AddNewCardCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();

        viewModel.SelectedBoard!.SearchText = "second";
        Dispatcher.UIThread.RunJobs();

        var visibleTitles = window.GetVisualDescendants()
            .OfType<CardView>()
            .Where(v => v.IsEffectivelyVisible)
            .Select(v => ((CardViewModel)v.DataContext!).Title)
            .ToList();
        Assert.Equal(new[] { "second" }, visibleTitles);
    }

    [AvaloniaFact]
    public async Task Clicking_a_card_with_the_mouse_opens_the_detail_panel()
    {
        var (window, viewModel) = await ShowWindowAsync();
        var cardView = window.GetVisualDescendants().OfType<CardView>().First();
        var center = cardView
            .TranslatePoint(new Point(cardView.Bounds.Width / 2, cardView.Bounds.Height / 2), window)!.Value;

        // 실제 사용자처럼: 누르고, 움직이지 않고 뗀다.
        window.MouseDown(center, MouseButton.Left);
        window.MouseUp(center, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.NotNull(viewModel.Detail);
    }

    [AvaloniaFact]
    public async Task Pressing_the_edit_button_does_not_open_the_detail_panel()
    {
        var (window, viewModel) = await ShowWindowAsync();
        var cardView = window.GetVisualDescendants().OfType<CardView>().First();
        var edit = cardView.GetVisualDescendants().OfType<Button>().First(b => Equals(b.Content, "✎"));
        var point = edit.TranslatePoint(new Point(edit.Bounds.Width / 2, edit.Bounds.Height / 2), window)!.Value;

        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Null(viewModel.Detail);
        Assert.True(viewModel.SelectedBoard!.Lists[0].Cards[0].IsEditing);
    }

    [AvaloniaFact]
    public async Task Sidebar_lists_boards_and_switching_shows_the_other_boards_cards()
    {
        var (window, viewModel) = await ShowWindowAsync();
        viewModel.NewBoardName = "Second";
        await viewModel.AddNewBoardCommand.ExecuteAsync(null);
        var second = viewModel.SelectedBoard!;
        second.Lists[0].NewCardTitle = "only in second";
        await second.Lists[0].AddNewCardCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();

        string[] VisibleCardTitles() => window.GetVisualDescendants()
            .OfType<CardView>()
            .Where(v => v.IsEffectivelyVisible)
            .Select(v => ((CardViewModel)v.DataContext!).Title)
            .ToArray();

        var boardNames = window.GetVisualDescendants().OfType<ListBox>().Single().ItemCount;
        Assert.Equal(2, boardNames);
        Assert.Equal(new[] { "only in second" }, VisibleCardTitles());

        viewModel.SelectedBoard = viewModel.Boards[0];
        Dispatcher.UIThread.RunJobs();

        Assert.DoesNotContain("only in second", VisibleCardTitles());
    }
}
