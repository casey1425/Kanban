using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Kanban.App;
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

    // ----- 사이드바로 카드 드래그 (헤드리스 드래그 앤 드롭 시뮬레이션) -----

    private static DataTransfer TransferOf(CardViewModel card)
    {
        // CardView가 드래그를 시작할 때 만드는 것과 같은 모양: 텍스트 + 앱 내부 형식
        var item = DataTransferItem.CreateText(card.Title);
        item.Set(DragFormats.Card, card);
        var transfer = new DataTransfer();
        transfer.Add(item);
        return transfer;
    }

    private static Point CenterOf(Visual target, Visual window) =>
        target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window)!.Value;

    [AvaloniaFact]
    public async Task Dropping_a_card_on_another_board_in_the_sidebar_moves_it_and_shows_a_notice()
    {
        var (window, viewModel) = await ShowWindowAsync();
        viewModel.NewBoardName = "Second";
        await viewModel.AddNewBoardCommand.ExecuteAsync(null);
        var second = viewModel.SelectedBoard!;
        viewModel.SelectedBoard = viewModel.Boards[0];
        Dispatcher.UIThread.RunJobs();

        var card = viewModel.SelectedBoard!.Lists[0].Cards[0];
        var secondItem = window.GetVisualDescendants().OfType<ListBoxItem>()
            .Single(i => ReferenceEquals(i.DataContext, second));
        var point = CenterOf(secondItem, window);
        var transfer = TransferOf(card);

        window.DragDrop(point, RawDragEventType.DragEnter, transfer, DragDropEffects.Move);
        window.DragDrop(point, RawDragEventType.DragOver, transfer, DragDropEffects.Move);
        Assert.Contains("drop-target", secondItem.Classes);   // 놓을 보드가 강조된다.

        window.DragDrop(point, RawDragEventType.Drop, transfer, DragDropEffects.Move);
        Dispatcher.UIThread.RunJobs();

        Assert.DoesNotContain("drop-target", secondItem.Classes);
        Assert.Contains(card, second.Lists[0].Cards);
        Assert.Same(second.Lists[0], card.Owner);
        Assert.Contains("Second", viewModel.InfoMessage);
    }

    [AvaloniaFact]
    public async Task Dragging_a_card_over_its_own_board_is_not_a_drop_target()
    {
        var (window, viewModel) = await ShowWindowAsync();
        viewModel.NewBoardName = "Second";
        await viewModel.AddNewBoardCommand.ExecuteAsync(null);
        viewModel.SelectedBoard = viewModel.Boards[0];
        Dispatcher.UIThread.RunJobs();

        var card = viewModel.SelectedBoard!.Lists[0].Cards[0];
        var ownItem = window.GetVisualDescendants().OfType<ListBoxItem>()
            .Single(i => ReferenceEquals(i.DataContext, viewModel.Boards[0]));
        var point = CenterOf(ownItem, window);
        var transfer = TransferOf(card);

        window.DragDrop(point, RawDragEventType.DragOver, transfer, DragDropEffects.Move);
        Assert.DoesNotContain("drop-target", ownItem.Classes);

        window.DragDrop(point, RawDragEventType.Drop, transfer, DragDropEffects.Move);
        Dispatcher.UIThread.RunJobs();

        Assert.Same(viewModel.Boards[0].Lists[0], card.Owner);
        Assert.Null(viewModel.InfoMessage);
    }

    // ----- 실행 취소 / 다시 실행 (단축키와 버튼) -----

    [AvaloniaFact]
    public async Task Ctrl_Z_undoes_and_Ctrl_Shift_Z_redoes_the_last_action()
    {
        var (window, viewModel) = await ShowWindowAsync();
        var list = viewModel.SelectedBoard!.Lists[0];
        list.NewCardTitle = "to be undone";
        await list.AddNewCardCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();
        Assert.Contains(list.Cards, c => c.Title == "to be undone");

        window.KeyPressQwerty(PhysicalKey.Z, RawInputModifiers.Control);
        window.KeyReleaseQwerty(PhysicalKey.Z, RawInputModifiers.Control);
        await WaitForIdleAsync(viewModel);
        Assert.DoesNotContain(list.Cards, c => c.Title == "to be undone");

        window.KeyPressQwerty(PhysicalKey.Z, RawInputModifiers.Control | RawInputModifiers.Shift);
        window.KeyReleaseQwerty(PhysicalKey.Z, RawInputModifiers.Control | RawInputModifiers.Shift);
        await WaitForIdleAsync(viewModel);
        Assert.Contains(list.Cards, c => c.Title == "to be undone");
    }

    [AvaloniaFact]
    public async Task Ctrl_Y_also_redoes()
    {
        var (window, viewModel) = await ShowWindowAsync();
        var list = viewModel.SelectedBoard!.Lists[0];
        list.NewCardTitle = "again";
        await list.AddNewCardCommand.ExecuteAsync(null);
        await viewModel.UndoCommand.ExecuteAsync(null);
        Assert.DoesNotContain(list.Cards, c => c.Title == "again");

        window.KeyPressQwerty(PhysicalKey.Y, RawInputModifiers.Control);
        window.KeyReleaseQwerty(PhysicalKey.Y, RawInputModifiers.Control);
        await WaitForIdleAsync(viewModel);

        Assert.Contains(list.Cards, c => c.Title == "again");
    }

    [AvaloniaFact]
    public async Task Undo_and_redo_buttons_follow_the_history()
    {
        var (window, viewModel) = await ShowWindowAsync();

        Button Find(string glyph) => window.GetVisualDescendants().OfType<Button>().Single(b => Equals(b.Content, glyph));

        Assert.False(Find("실행 취소").IsEffectivelyEnabled);   // 처음에는 되돌릴 게 없다
        Assert.False(Find("다시 실행").IsEffectivelyEnabled);

        var list = viewModel.SelectedBoard!.Lists[0];
        list.NewCardTitle = "x";
        await list.AddNewCardCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();
        Assert.True(Find("실행 취소").IsEffectivelyEnabled);
        Assert.False(Find("다시 실행").IsEffectivelyEnabled);

        await viewModel.UndoCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();
        Assert.False(Find("실행 취소").IsEffectivelyEnabled);
        Assert.True(Find("다시 실행").IsEffectivelyEnabled);
    }

    // 키 입력으로 시작된 비동기 명령(되돌리기)이 끝날 때까지 기다린다.
    private static async Task WaitForIdleAsync(MainWindowViewModel viewModel)
    {
        for (var i = 0; i < 100; i++)
        {
            Dispatcher.UIThread.RunJobs();
            if (viewModel.UndoCommand.IsRunning is false && viewModel.RedoCommand.IsRunning is false)
                break;
            await Task.Delay(10);
        }
        await Task.Delay(30);
        Dispatcher.UIThread.RunJobs();
    }
}
