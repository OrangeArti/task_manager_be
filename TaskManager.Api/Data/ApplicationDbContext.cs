using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TaskManager.Api.Models;

namespace TaskManager.Api.Data
{
    // IMPORTANT: inherit from IdentityDbContext<ApplicationUser>
	public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
	{
		public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
			: base(options) { }

		public DbSet<TaskItem> Tasks => Set<TaskItem>();

		public DbSet<Team> Teams => Set<Team>();

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
            base.OnModelCreating(modelBuilder); // keep this to register Identity tables

			modelBuilder.Entity<TaskItem>(e =>
			{
				e.HasKey(t => t.Id);
				e.Property(t => t.Title).HasMaxLength(200).IsRequired();
				e.Property(t => t.Description).HasMaxLength(4000);
                e.Property(t => t.Priority).HasDefaultValue(0);
                e.Property(t => t.IsCompleted).HasDefaultValue(false);
                e.Property(t => t.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
                e.Property(t => t.IsAssigneeVisibleToOthers).HasDefaultValue(true);
                e.Property(t => t.VisibilityScope)
                    .HasMaxLength(32)
                    .HasDefaultValue(TaskVisibilityScopes.Private);
                e.Property(t => t.CreatedById)
                    .HasMaxLength(450)
                    .IsRequired();
                e.HasIndex(t => t.CreatedById);
                e.HasOne(t => t.CreatedBy)
                    .WithMany()
                    .HasForeignKey(t => t.CreatedById)
                    .OnDelete(DeleteBehavior.Restrict);
                e.HasIndex(t => t.AssignedToId);
                e.HasOne(t => t.AssignedTo)
                    .WithMany()
                    .HasForeignKey(t => t.AssignedToId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasIndex(t => t.TeamId);
                e.HasOne(t => t.Team)
                    .WithMany()
                    .HasForeignKey(t => t.TeamId)
                    .OnDelete(DeleteBehavior.SetNull);
				e.Property(t => t.IsProblem).HasDefaultValue(false);
				e.Property(t => t.ProblemDescription).HasMaxLength(2000);
				e.Property(t => t.ProblemReporterId).HasMaxLength(450);
				e.HasIndex(t => t.IsProblem);
			});

			modelBuilder.Entity<Team>(entity =>
			{
				entity.HasKey(t => t.Id);
				entity.Property(t => t.Name)
					.HasMaxLength(100)
					.IsRequired();
				entity.Property(t => t.CreatedAt)
					.HasDefaultValueSql("GETUTCDATE()");
				entity.HasMany(t => t.Members)
					.WithOne(u => u.Team)
					.HasForeignKey(u => u.TeamId)
					.OnDelete(DeleteBehavior.SetNull);
			});
		}
	}
}
