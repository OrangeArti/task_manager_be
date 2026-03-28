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

		public DbSet<Organization> Organizations => Set<Organization>();
		public DbSet<Subscription> Subscriptions => Set<Subscription>();
		public DbSet<Group> Groups => Set<Group>();
		public DbSet<GroupMember> GroupMembers => Set<GroupMember>();
		public DbSet<Comment> Comments => Set<Comment>();

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
                e.Property(t => t.GroupId);
                e.HasOne(t => t.Group)
                    .WithMany(g => g.Tasks)
                    .HasForeignKey(t => t.GroupId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasIndex(t => t.GroupId);
			});

			modelBuilder.Entity<ApplicationUser>()
				.HasIndex(u => u.KeycloakSubject)
				.IsUnique(false);

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

            // Organization
            modelBuilder.Entity<Organization>(o =>
            {
                o.HasKey(x => x.Id);
                o.Property(x => x.Name).HasMaxLength(200).IsRequired();
                o.Property(x => x.OwnerId).HasMaxLength(450).IsRequired();
                o.Property(x => x.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
                o.HasOne(x => x.Owner)
                    .WithMany()
                    .HasForeignKey(x => x.OwnerId)
                    .OnDelete(DeleteBehavior.Restrict);
                o.HasIndex(x => x.OwnerId);
            });

            // Subscription (1-to-1 with Organization)
            modelBuilder.Entity<Subscription>(s =>
            {
                s.HasKey(x => x.Id);
                s.Property(x => x.PlanType).HasMaxLength(50).HasDefaultValue("Free");
                s.Property(x => x.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
                s.HasOne(x => x.Organization)
                    .WithOne(o => o.Subscription)
                    .HasForeignKey<Subscription>(x => x.OrganizationId)
                    .OnDelete(DeleteBehavior.Cascade);
                s.HasIndex(x => x.OrganizationId).IsUnique();
            });

            // Group
            modelBuilder.Entity<Group>(g =>
            {
                g.HasKey(x => x.Id);
                g.Property(x => x.Name).HasMaxLength(100).IsRequired();
                g.Property(x => x.Description).HasMaxLength(500);
                g.Property(x => x.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
                g.HasOne(x => x.Organization)
                    .WithMany(o => o.Groups)
                    .HasForeignKey(x => x.OrganizationId)
                    .OnDelete(DeleteBehavior.Cascade);
                g.HasIndex(x => x.OrganizationId);
            });

            // GroupMember
            modelBuilder.Entity<GroupMember>(gm =>
            {
                gm.HasKey(x => x.Id);
                gm.Property(x => x.UserId).HasMaxLength(450).IsRequired();
                gm.Property(x => x.JoinedAt).HasDefaultValueSql("GETUTCDATE()");
                gm.HasOne(x => x.Group)
                    .WithMany(g => g.Members)
                    .HasForeignKey(x => x.GroupId)
                    .OnDelete(DeleteBehavior.Cascade);
                gm.HasOne(x => x.User)
                    .WithMany()
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
                gm.HasIndex(x => new { x.GroupId, x.UserId }).IsUnique();
            });

            // Comment
            modelBuilder.Entity<Comment>(c =>
            {
                c.HasKey(x => x.Id);
                c.Property(x => x.AuthorId).HasMaxLength(450).IsRequired();
                c.Property(x => x.Content).HasMaxLength(4000).IsRequired();
                c.Property(x => x.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
                c.HasOne(x => x.Task)
                    .WithMany()
                    .HasForeignKey(x => x.TaskId)
                    .OnDelete(DeleteBehavior.Cascade);
                c.HasOne(x => x.Author)
                    .WithMany()
                    .HasForeignKey(x => x.AuthorId)
                    .OnDelete(DeleteBehavior.Restrict);
                c.HasIndex(x => x.TaskId);
                c.HasIndex(x => x.AuthorId);
            });
		}
	}
}
