using Kanban.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Kanban.Data;

public class KanbanDbContext : DbContext
{
    public KanbanDbContext(DbContextOptions<KanbanDbContext> options) : base(options)
    {
    }

    public DbSet<BoardList> Lists => Set<BoardList>();

    public DbSet<Card> Cards => Set<Card>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BoardList>(e =>
        {
            e.Property(l => l.Name).IsRequired().HasMaxLength(100);
            e.HasMany(l => l.Cards)
                .WithOne(c => c.List)
                .HasForeignKey(c => c.ListId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Card>(e =>
        {
            e.Property(c => c.Title).IsRequired().HasMaxLength(200);
            e.HasIndex(c => new { c.ListId, c.Position });
        });
    }
}
