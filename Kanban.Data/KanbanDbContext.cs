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

    public DbSet<Label> Labels => Set<Label>();

    public DbSet<ChecklistItem> ChecklistItems => Set<ChecklistItem>();

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
            e.HasMany(c => c.Labels).WithMany(l => l.Cards); // 다대다: 조인 테이블은 EF가 만든다.
        });

        modelBuilder.Entity<ChecklistItem>(e =>
        {
            e.Property(i => i.Text).IsRequired().HasMaxLength(200);
            e.HasOne(i => i.Card)
                .WithMany(c => c.ChecklistItems)
                .HasForeignKey(i => i.CardId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Label>(e =>
        {
            e.Property(l => l.Name).IsRequired().HasMaxLength(30);
            e.Property(l => l.Color).IsRequired().HasMaxLength(9);

            // 기본 라벨. 라벨 편집 UI가 생기기 전까지는 고정 세트를 쓴다.
            e.HasData(
                new Label { Id = 1, Name = "긴급", Color = "#E5484D" },
                new Label { Id = 2, Name = "중요", Color = "#F76B15" },
                new Label { Id = 3, Name = "기능", Color = "#3E63DD" },
                new Label { Id = 4, Name = "버그", Color = "#8E4EC6" },
                new Label { Id = 5, Name = "문서", Color = "#30A46C" },
                new Label { Id = 6, Name = "아이디어", Color = "#B7791F" });
        });
    }
}
