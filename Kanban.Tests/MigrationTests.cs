using Kanban.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Data.Sqlite;
using Kanban.Tests.Support;

namespace Kanban.Tests;

/// <summary>
/// 이미 앱을 쓰던 사용자의 DB를 새 버전으로 올려도 데이터가 보존되는지 확인한다.
/// 마이그레이션 코드를 직접 고친 경우(AddBoards)에 특히 중요하다.
/// </summary>
public sealed class MigrationTests : IDisposable
{
    private readonly TestApp _app = new();

    public void Dispose() => _app.Dispose();

    private KanbanDbContext OpenContext() =>
        new(new DbContextOptionsBuilder<KanbanDbContext>()
            .UseSqlite($"Data Source={_app.DatabasePath}")
            .Options);

    private static long Scalar(KanbanDbContext db, string sql)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(command.ExecuteScalar());
    }

    [Fact]
    public void Upgrading_from_the_pre_board_schema_keeps_all_data_and_attaches_lists_to_a_default_board()
    {
        // 1) "보드" 기능이 생기기 전 스키마(AddChecklist까지)로 DB를 만들고 데이터를 넣는다.
        using (var old = OpenContext())
        {
            old.GetService<IMigrator>().Migrate("AddChecklist");

            old.Database.ExecuteSqlRaw("INSERT INTO Lists (Name, Position) VALUES ('To Do', 0), ('Done', 1)");
            old.Database.ExecuteSqlRaw(
                "INSERT INTO Cards (Title, Description, Position, ListId, DueDate) VALUES " +
                "('A', 'desc', 0, 1, '2030-01-02'), ('B', NULL, 1, 1, NULL), ('C', NULL, 0, 2, NULL)");
            old.Database.ExecuteSqlRaw("INSERT INTO CardLabel (CardsId, LabelsId) VALUES (1, 1), (1, 3)");
            old.Database.ExecuteSqlRaw(
                "INSERT INTO ChecklistItems (Text, IsDone, Position, CardId) VALUES ('one', 1, 0, 1), ('two', 0, 1, 1)");
        }

        SqliteConnection.ClearAllPools();

        // 2) 최신 스키마로 올린다.
        using var upgraded = OpenContext();
        upgraded.Database.Migrate();

        // 3) 보드가 하나 생기고, 기존 리스트가 모두 거기에 속하며, 나머지 데이터는 그대로다.
        Assert.Equal(1, Scalar(upgraded, "SELECT COUNT(*) FROM Boards"));
        Assert.Equal(2, Scalar(upgraded, "SELECT COUNT(*) FROM Lists WHERE BoardId = (SELECT Id FROM Boards)"));
        Assert.Equal(3, Scalar(upgraded, "SELECT COUNT(*) FROM Cards"));
        Assert.Equal(2, Scalar(upgraded, "SELECT COUNT(*) FROM CardLabel"));
        Assert.Equal(2, Scalar(upgraded, "SELECT COUNT(*) FROM ChecklistItems"));
        Assert.Equal(0, Scalar(upgraded, "SELECT COUNT(*) FROM pragma_foreign_key_check"));
    }
}
