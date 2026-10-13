using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Kanban.Data;

/// <summary>`dotnet ef` 도구 전용. 마이그레이션을 만들 때만 쓰이고 앱 실행에는 관여하지 않는다.</summary>
public class KanbanDbContextFactory : IDesignTimeDbContextFactory<KanbanDbContext>
{
    public KanbanDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<KanbanDbContext>()
            .UseSqlite("Data Source=design-time.db")
            .Options;
        return new KanbanDbContext(options);
    }
}
