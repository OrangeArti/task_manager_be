using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TaskManager.Api.Models;

namespace TaskManager.Api.Data
{
    // ВАЖНО: наследуемся от IdentityDbContext<ApplicationUser>
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        public DbSet<TaskItem> Tasks => Set<TaskItem>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder); // не убирать, это регистрирует таблицы Identity

            modelBuilder.Entity<TaskItem>(e =>
            {
                e.HasKey(t => t.Id);
                e.Property(t => t.Title).HasMaxLength(200).IsRequired();
                e.Property(t => t.Description).HasMaxLength(4000);
                e.Property(t => t.Priority).HasDefaultValue(0);
                e.Property(t => t.IsCompleted).HasDefaultValue(false);
                e.Property(t => t.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
                e.Property(t => t.IsPublic).HasDefaultValue(false);
                e.HasIndex(t => t.OwnerId);
                e.HasOne(t => t.Owner)
                    .WithMany()
                    .HasForeignKey(t => t.OwnerId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.Property(t => t.IsProblem).HasDefaultValue(false);
                e.Property(t => t.ProblemDescription).HasMaxLength(2000);
                e.Property(t => t.ProblemReporterId).HasMaxLength(450);
                e.HasIndex(t => t.IsProblem);
            });
        }
    }
}