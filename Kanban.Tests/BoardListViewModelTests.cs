using Kanban.App.ViewModels;
using Kanban.Core.Models;
using Kanban.Tests.Support;

namespace Kanban.Tests;

/// <summary>카드 이동 규칙(InsertCard). 화면 상태만 다루므로 저장소는 호출되지 않는다.</summary>
public sealed class BoardListViewModelTests : IDisposable
{
    private readonly TestApp _app = new();
    private int _nextCardId = 1;

    public void Dispose() => _app.Dispose();

    private BoardListViewModel CreateList(string name, params string[] cardTitles)
    {
        var model = new BoardList
        {
            Id = 1,
            Name = name,
            Cards = cardTitles
                .Select((title, i) => new Card { Id = _nextCardId++, Title = title, Position = i })
                .ToList(),
        };
        return new BoardListViewModel(model, _app.CreateRepository(), Array.Empty<Label>(), _ => { });
    }

    private static string Order(BoardListViewModel list) => string.Join(",", list.Cards.Select(c => c.Title));

    [Theory]
    [InlineData(0, 3, "B,C,A")] // A를 C 뒤로
    [InlineData(2, 0, "C,A,B")] // C를 A 앞으로
    [InlineData(1, 1, "A,B,C")] // B를 자기 바로 앞에 -> 변화 없음
    [InlineData(1, 2, "A,B,C")] // B를 자기 바로 뒤에 -> 변화 없음
    [InlineData(0, 2, "B,A,C")] // A를 B 뒤로
    public void Moving_within_a_list_reorders_cards(int fromIndex, int insertIndex, string expected)
    {
        var list = CreateList("L", "A", "B", "C");

        list.InsertCard(list.Cards[fromIndex], insertIndex);

        Assert.Equal(expected, Order(list));
    }

    [Fact]
    public void Moving_to_another_list_changes_owner_and_order()
    {
        var source = CreateList("From", "1", "2");
        var target = CreateList("To", "x");
        var card = source.Cards[0];

        target.InsertCard(card, 0);

        Assert.Equal("2", Order(source));
        Assert.Equal("1,x", Order(target));
        Assert.Same(target, card.Owner);
    }

    [Fact]
    public void Out_of_range_index_is_clamped()
    {
        var source = CreateList("From", "1");
        var empty = CreateList("Empty");

        empty.InsertCard(source.Cards[0], 99);

        Assert.Equal("1", Order(empty));
        Assert.Empty(source.Cards);
    }

    [Fact]
    public void RemoveCard_clears_owner()
    {
        var list = CreateList("L", "A");
        var card = list.Cards[0];

        list.RemoveCard(card);

        Assert.Empty(list.Cards);
        Assert.Null(card.Owner);
    }
}
