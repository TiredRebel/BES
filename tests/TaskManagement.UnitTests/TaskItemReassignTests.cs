using TaskManagement.Domain;

namespace TaskManagement.UnitTests;

/// <summary>
/// Unit tests for <see cref="TaskItem.Reassign(Employee)"/>.
/// </summary>
public sealed class TaskItemReassignTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    private static Employee CreateCreator() => Employee.Create("Creator", "creator@example.com");

    private static Employee CreateAssignee(string name = "Assignee") =>
        Employee.Create(name, $"{name.ToLowerInvariant()}@example.com");

    /// <summary>Updates assignee ID when assigned to a new active employee.</summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Reassign_ValidActiveEmployee_UpdatesAssigneeId()
    {
        var creator = CreateCreator();
        var initialAssignee = CreateAssignee("Bob");
        var newAssignee = CreateAssignee("Charlie");
        var task = TaskItem.Create("Task", creator, initialAssignee, null, null);

        task.Reassign(newAssignee);

        Assert.Equal(newAssignee.Id, task.AssigneeId);
    }

    /// <summary>Reassigning to the same assignee is a no-op.</summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Reassign_SameAssignee_IsIdempotentAndKeepsAssigneeId()
    {
        var creator = CreateCreator();
        var assignee = CreateAssignee("Bob");
        var task = TaskItem.Create("Task", creator, assignee, null, null);

        task.Reassign(assignee);

        Assert.Equal(assignee.Id, task.AssigneeId);
    }

    /// <summary>Throws ArgumentNullException when new assignee is null.</summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Reassign_NullAssignee_ThrowsArgumentNullException()
    {
        var task = TaskItem.Create("Task", CreateCreator(), CreateAssignee(), null, null);

        Assert.Throws<ArgumentNullException>("newAssignee", () => task.Reassign(null!));
    }

    /// <summary>Cannot reassign completed task.</summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Reassign_TaskCompleted_ThrowsBusinessRuleViolationExceptionBR4()
    {
        var creator = CreateCreator();
        var task = TaskItem.Create("Task", creator, CreateAssignee("Bob"), null, null);
        task.ChangeStatus(TaskItemStatus.Completed, T0);

        var newAssignee = CreateAssignee("Charlie");
        var ex = Assert.Throws<BusinessRuleViolationException>(() => task.Reassign(newAssignee));

        Assert.Equal("BR4", ex.RuleId);
    }

    /// <summary>Cannot reassign cancelled task.</summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Reassign_TaskCancelled_ThrowsBusinessRuleViolationExceptionBR4()
    {
        var creator = CreateCreator();
        var task = TaskItem.Create("Task", creator, CreateAssignee("Bob"), null, null);
        task.ChangeStatus(TaskItemStatus.Cancelled, T0);

        var newAssignee = CreateAssignee("Charlie");
        var ex = Assert.Throws<BusinessRuleViolationException>(() => task.Reassign(newAssignee));

        Assert.Equal("BR4", ex.RuleId);
    }

    /// <summary>Cannot reassign task to its creator.</summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Reassign_CreatorAsNewAssignee_ThrowsBusinessRuleViolationExceptionBR5()
    {
        var creator = CreateCreator();
        var task = TaskItem.Create("Task", creator, CreateAssignee("Bob"), null, null);

        var ex = Assert.Throws<BusinessRuleViolationException>(() => task.Reassign(creator));

        Assert.Equal("BR5", ex.RuleId);
    }

    /// <summary>Cannot reassign task to inactive employee.</summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Reassign_InactiveEmployee_ThrowsBusinessRuleViolationExceptionBR3()
    {
        var creator = CreateCreator();
        var task = TaskItem.Create("Task", creator, CreateAssignee("Bob"), null, null);
        var inactiveAssignee = CreateAssignee("Charlie");
        inactiveAssignee.Deactivate();

        var ex = Assert.Throws<BusinessRuleViolationException>(() => task.Reassign(inactiveAssignee));

        Assert.Equal("BR3", ex.RuleId);
    }

    /// <summary>Completed status check executes before creator and active checks.</summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Reassign_CompletedTaskWithInactiveCreator_ReportsBR4First()
    {
        var creator = CreateCreator();
        var task = TaskItem.Create("Task", creator, CreateAssignee("Bob"), null, null);
        task.ChangeStatus(TaskItemStatus.Completed, T0);
        creator.Deactivate();

        var ex = Assert.Throws<BusinessRuleViolationException>(() => task.Reassign(creator));

        Assert.Equal("BR4", ex.RuleId);
    }
}
