using Kanban.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kanban.Data;

public static class ServiceCollectionExtensions
{
    /// <summary>SQLite 기반 저장소를 DI 컨테이너에 등록한다.</summary>
    public static IServiceCollection AddKanbanData(this IServiceCollection services, string databasePath)
    {
        services.AddDbContextFactory<KanbanDbContext>(o => o.UseSqlite($"Data Source={databasePath}"));
        services.AddSingleton<IBoardRepository, BoardRepository>();
        return services;
    }
}
