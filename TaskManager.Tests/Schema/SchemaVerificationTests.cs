using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TaskManager.Api.Data;
using TaskManager.Api.Models;

namespace TaskManager.Tests.Schema;

/// <summary>
/// Schema contract tests for Phase 2 (Multi-Tenant Schema Foundation).
///
/// RED state: Tests fail until Plan 02-02 creates entity classes and registers DbSets.
/// GREEN state: All tests pass after Plan 02-02 completes.
///
/// Covers: SUB-04 (entities persisted in SQL Server), QA-02 (migration foundation).
/// </summary>
public class SchemaVerificationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public SchemaVerificationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // ── EF Metadata Tests (compile fine, fail at runtime until DbSets registered) ──

    [Fact]
    public void Organizations_EntityType_IsRegisteredInDbContext()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var entityTypeNames = db.Model.GetEntityTypes().Select(e => e.ClrType.Name).ToList();
        Assert.Contains("Organization", entityTypeNames);
    }

    [Fact]
    public void Subscriptions_EntityType_IsRegisteredInDbContext()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var entityTypeNames = db.Model.GetEntityTypes().Select(e => e.ClrType.Name).ToList();
        Assert.Contains("Subscription", entityTypeNames);
    }

    [Fact]
    public void Groups_EntityType_IsRegisteredInDbContext()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var entityTypeNames = db.Model.GetEntityTypes().Select(e => e.ClrType.Name).ToList();
        Assert.Contains("Group", entityTypeNames);
    }

    [Fact]
    public void GroupMembers_EntityType_IsRegisteredInDbContext()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var entityTypeNames = db.Model.GetEntityTypes().Select(e => e.ClrType.Name).ToList();
        Assert.Contains("GroupMember", entityTypeNames);
    }

    [Fact]
    public void Comments_EntityType_IsRegisteredInDbContext()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var entityTypeNames = db.Model.GetEntityTypes().Select(e => e.ClrType.Name).ToList();
        Assert.Contains("Comment", entityTypeNames);
    }

    [Fact]
    public void TaskItem_HasGroupId_Property_InEfModel()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var taskItemType = db.Model.FindEntityType(typeof(TaskItem));
        Assert.NotNull(taskItemType);
        var groupIdProp = taskItemType.FindProperty("GroupId");
        Assert.NotNull(groupIdProp);
        Assert.True(groupIdProp.IsNullable, "GroupId must be nullable (additive FK)");
    }

    // ── Entity Instantiation Tests (compile error = RED until Plan 02-02 creates classes) ──

    [Fact]
    public void Organization_CanBeInstantiated_WithRequiredProperties()
    {
        var org = new Organization { Name = "Acme Corp", OwnerId = "user1" };
        Assert.NotNull(org);
        Assert.Equal("Acme Corp", org.Name);
    }

    [Fact]
    public void Group_CanBeInstantiated_WithRequiredProperties()
    {
        var group = new Group { Name = "Engineering", OrganizationId = 1 };
        Assert.NotNull(group);
        Assert.Equal("Engineering", group.Name);
        Assert.Equal(1, group.OrganizationId);
    }

    [Fact]
    public void GroupMember_CanBeInstantiated_WithRequiredProperties()
    {
        var member = new GroupMember { GroupId = 1, UserId = "user1" };
        Assert.NotNull(member);
        Assert.Equal(1, member.GroupId);
        Assert.Equal("user1", member.UserId);
    }

    [Fact]
    public void Comment_CanBeInstantiated_WithRequiredProperties()
    {
        var comment = new Comment { TaskId = 1, AuthorId = "user1", Content = "Looks good!" };
        Assert.NotNull(comment);
        Assert.Equal("Looks good!", comment.Content);
    }

    [Fact]
    public void Subscription_CanBeInstantiated_WithPlanType()
    {
        var sub = new Subscription { PlanType = "Free", OrganizationId = 1 };
        Assert.NotNull(sub);
        Assert.Equal("Free", sub.PlanType);
    }
}
