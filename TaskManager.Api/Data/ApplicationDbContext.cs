using Microsoft.EntityFrameworkCore;
using TaskManager.Api.Models;

namespace TaskManager.Api.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }
        public DbSet<TaskItem> Task => Set<TaskItem>();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<TaskItem>(e =>
            {
                e.ToTable("Tasks");
                e.HasKey(x => x.Id);
                e.Property(x => x.Title).IsRequired().HasMaxLength(200);
                e.Property(x => x.Description).HasMaxLength(4000);
                e.Property(x => x.Priority).HasDefaultValue(0);
                e.Property(x => x.IsCompleted).HasDefaultValue(false);
                e.Property(x => x.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            }
            );
        }
    }
}
