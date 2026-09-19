using TaskManagement.Domain;

namespace TaskManagement.UnitTests;

/// <summary>
/// Unit tests for <see cref="TaskItem.ChangeStatus(TaskItemStatus, DateTimeOffset)"/>, covering BR1, BR4 and the
/// full status transition table.
/// </summary>
public sealed class TaskItemChangeStatusTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    private static TaskItem CreateNewTask()
    {
        var creator = Employee.Create("Creator", "creator@example.com");
        var assignee = Employee.Create("Assignee", "assignee@example.com");

        return TaskItem.Create("Prepare report", creator, assignee, null, null);
    }

    /// <summary>
    /// Verifies every row of the status transition table (spec §5): a change is allowed iff the current status is
    /// not final, and an allowed change to <see cref="TaskItemStatus.Completed"/> sets <c>CompletedAt</c> while a
    /// disallowed change leaves <c>Status</c> and <c>CompletedAt</c> unchanged. Proves BR4 (finality) and BR1
    /// (<c>CompletedAt</c> set iff Completed).
    /// </summary>
    /// <param name="from">The status the task is put into before the change under test (skipped for <c>New</c>,
    /// which is the status a new task already has).</param>
    /// <param name="to">The status passed to <see cref="TaskItem.ChangeStatus(TaskItemStatus, DateTimeOffset)"/>.</param>
    /// <param name="allowed">Whether the transition from <paramref name="from"/> to <paramref name="to"/> is
    /// allowed.</param>
    [Theory]
    [InlineData(TaskItemStatus.New, TaskItemStatus.New, true)]
    [InlineData(TaskItemStatus.New, TaskItemStatus.InProgress, true)]
    [InlineData(TaskItemStatus.New, TaskItemStatus.Completed, true)]
    [InlineData(TaskItemStatus.New, TaskItemStatus.Cancelled, true)]
    [InlineData(TaskItemStatus.InProgress, TaskItemStatus.New, true)]
    [InlineData(TaskItemStatus.InProgress, TaskItemStatus.InProgress, true)]
    [InlineData(TaskItemStatus.InProgress, TaskItemStatus.Completed, true)]
    [InlineData(TaskItemStatus.InProgress, TaskItemStatus.Cancelled, true)]
    [InlineData(TaskItemStatus.Completed, TaskItemStatus.New, false)]
    [InlineData(TaskItemStatus.Completed, TaskItemStatus.InProgress, false)]
    [InlineData(TaskItemStatus.Completed, TaskItemStatus.Completed, false)]
    [InlineData(TaskItemStatus.Completed, TaskItemStatus.Cancelled, false)]
    [InlineData(TaskItemStatus.Cancelled, TaskItemStatus.New, false)]
    [InlineData(TaskItemStatus.Cancelled, TaskItemStatus.InProgress, false)]
    [InlineData(TaskItemStatus.Cancelled, TaskItemStatus.Completed, false)]
    [InlineData(TaskItemStatus.Cancelled, TaskItemStatus.Cancelled, false)]
    [Trait("Category", "Unit")]
    public void ChangeStatus_TransitionTableRow_BehavesAsSpecified(TaskItemStatus from, TaskItemStatus to, bool allowed)
    {
        var task = CreateNewTask();
        if (from != TaskItemStatus.New)
        {
            task.ChangeStatus(from, T0);
        }

        var changedAt = T0.AddHours(1);

        if (allowed)
        {
            task.ChangeStatus(to, changedAt);

            Assert.Equal(to, task.Status);
            Assert.Equal(to == TaskItemStatus.Completed ? changedAt : null, task.CompletedAt);
        }
        else
        {
            var statusBeforeChange = task.Status;
            var completedAtBeforeChange = task.CompletedAt;

            var exception = Assert.Throws<BusinessRuleViolationException>(() => task.ChangeStatus(to, changedAt));

            Assert.Equal("BR4", exception.RuleId);
            Assert.Equal(statusBeforeChange, task.Status);
            Assert.Equal(completedAtBeforeChange, task.CompletedAt);
        }
    }

    /// <summary>
    /// Verifies that <see cref="TaskItem.ChangeStatus(TaskItemStatus, DateTimeOffset)"/> sets <c>CompletedAt</c>
    /// to <c>changedAt</c> when the new status is <see cref="TaskItemStatus.Completed"/>. Proves BR1.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void ChangeStatus_ToCompleted_SetsCompletedAtToChangedAt()
    {
        var task = CreateNewTask();
        var changedAt = T0.AddDays(1);

        task.ChangeStatus(TaskItemStatus.Completed, changedAt);

        Assert.Equal(TaskItemStatus.Completed, task.Status);
        Assert.Equal(changedAt, task.CompletedAt);
    }

    /// <summary>
    /// Verifies that <see cref="TaskItem.ChangeStatus(TaskItemStatus, DateTimeOffset)"/> normalises a non-UTC
    /// <c>changedAt</c> to the same instant with a zero offset when storing it in <c>CompletedAt</c>. Proves BR1's
    /// <c>CompletedAt</c> is set on completion, in UTC.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void ChangeStatus_ToCompletedWithNonUtcOffset_StoresUtcInstant()
    {
        var task = CreateNewTask();
        var changedAt = new DateTimeOffset(2026, 10, 1, 11, 0, 0, TimeSpan.FromHours(2));

        task.ChangeStatus(TaskItemStatus.Completed, changedAt);

        Assert.Equal(TimeSpan.Zero, task.CompletedAt!.Value.Offset);
        Assert.Equal(changedAt.ToUniversalTime(), task.CompletedAt);
    }

    /// <summary>
    /// Verifies that <see cref="TaskItem.ChangeStatus(TaskItemStatus, DateTimeOffset)"/> throws
    /// <see cref="BusinessRuleViolationException"/> with <c>RuleId == "BR4"</c> and leaves <c>Status</c> and
    /// <c>CompletedAt</c> unchanged when the task is already Completed. Proves BR4 and BR1 (the frozen value).
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void ChangeStatus_FromCompleted_KeepsStatusAndCompletedAt()
    {
        var task = CreateNewTask();
        task.ChangeStatus(TaskItemStatus.Completed, T0);

        var exception = Assert.Throws<BusinessRuleViolationException>(
            () => task.ChangeStatus(TaskItemStatus.InProgress, T0.AddDays(1)));

        Assert.Equal("BR4", exception.RuleId);
        Assert.Equal(TaskItemStatus.Completed, task.Status);
        Assert.Equal(T0, task.CompletedAt);
    }

    /// <summary>
    /// Verifies that <see cref="TaskItem.ChangeStatus(TaskItemStatus, DateTimeOffset)"/> throws
    /// <see cref="ArgumentOutOfRangeException"/> for a <c>newStatus</c> that is not a defined
    /// <see cref="TaskItemStatus"/> member.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void ChangeStatus_UndefinedStatus_ThrowsArgumentOutOfRangeException()
    {
        var task = CreateNewTask();

        Assert.Throws<ArgumentOutOfRangeException>(() => task.ChangeStatus((TaskItemStatus)99, T0));
    }
}
