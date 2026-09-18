using TaskManagement.Domain;

namespace TaskManagement.UnitTests;

/// <summary>
/// Unit tests for <see cref="TaskItem.Create(string, Employee, Employee, DateTimeOffset?, DateTimeOffset?)"/>,
/// covering BR1, BR2, BR3, BR5 and UTC time normalisation.
/// </summary>
public sealed class TaskItemCreateTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    private static Employee CreateCreator() => Employee.Create("Creator", "creator@example.com");

    private static Employee CreateAssignee() => Employee.Create("Assignee", "assignee@example.com");

    /// <summary>
    /// Verifies that <see cref="TaskItem.Create(string, Employee, Employee, DateTimeOffset?, DateTimeOffset?)"/>
    /// starts a new task in <see cref="TaskItemStatus.New"/> with a null <c>CompletedAt</c>. Proves BR1: a task
    /// that is not Completed has no <c>CompletedAt</c>.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Create_ValidInput_StartsAsNewWithNullCompletedAt()
    {
        var task = TaskItem.Create("Prepare report", CreateCreator(), CreateAssignee(), null, null);

        Assert.Equal(TaskItemStatus.New, task.Status);
        Assert.Null(task.CompletedAt);
    }

    /// <summary>
    /// Verifies that <see cref="TaskItem.Create(string, Employee, Employee, DateTimeOffset?, DateTimeOffset?)"/>
    /// copies the title, the creator and assignee ids, and the planned start and due dates onto the new task.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Create_ValidInput_CopiesTitleIdsAndDates()
    {
        var creator = CreateCreator();
        var assignee = CreateAssignee();
        var dueAt = T0.AddDays(5);

        var task = TaskItem.Create("Prepare report", creator, assignee, T0, dueAt);

        Assert.Equal("Prepare report", task.Title);
        Assert.Equal(creator.Id, task.CreatorId);
        Assert.Equal(assignee.Id, task.AssigneeId);
        Assert.Equal(T0, task.PlannedStartAt);
        Assert.Equal(dueAt, task.DueAt);
    }

    /// <summary>
    /// Verifies that <see cref="TaskItem.Create(string, Employee, Employee, DateTimeOffset?, DateTimeOffset?)"/>
    /// trims surrounding whitespace from <c>title</c>.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Create_TitleWithSurroundingSpaces_StoresTrimmedTitle()
    {
        var task = TaskItem.Create("  Prepare report  ", CreateCreator(), CreateAssignee(), null, null);

        Assert.Equal("Prepare report", task.Title);
    }

    /// <summary>
    /// Verifies that <see cref="TaskItem.Create(string, Employee, Employee, DateTimeOffset?, DateTimeOffset?)"/>
    /// throws <see cref="ArgumentNullException"/> for a null <c>title</c>.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Create_NullTitle_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(
            () => TaskItem.Create(null!, CreateCreator(), CreateAssignee(), null, null));
    }

    /// <summary>
    /// Verifies that <see cref="TaskItem.Create(string, Employee, Employee, DateTimeOffset?, DateTimeOffset?)"/>
    /// throws <see cref="ArgumentException"/> for an empty or whitespace-only <c>title</c>.
    /// </summary>
    /// <param name="title">The blank title under test.</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [Trait("Category", "Unit")]
    public void Create_BlankTitle_ThrowsArgumentException(string title)
    {
        Assert.Throws<ArgumentException>(
            () => TaskItem.Create(title, CreateCreator(), CreateAssignee(), null, null));
    }

    /// <summary>
    /// Verifies that <see cref="TaskItem.Create(string, Employee, Employee, DateTimeOffset?, DateTimeOffset?)"/>
    /// throws <see cref="ArgumentException"/> when <c>title</c> is one character past the 200-character boundary.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Create_TitleOf201Chars_ThrowsArgumentException()
    {
        var title = new string('a', TaskItem.TitleMaxLength + 1);

        Assert.Throws<ArgumentException>(
            () => TaskItem.Create(title, CreateCreator(), CreateAssignee(), null, null));
    }

    /// <summary>
    /// Verifies that <see cref="TaskItem.Create(string, Employee, Employee, DateTimeOffset?, DateTimeOffset?)"/>
    /// throws <see cref="ArgumentNullException"/> for a null <c>creator</c>.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Create_NullCreator_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(
            () => TaskItem.Create("Prepare report", null!, CreateAssignee(), null, null));
    }

    /// <summary>
    /// Verifies that <see cref="TaskItem.Create(string, Employee, Employee, DateTimeOffset?, DateTimeOffset?)"/>
    /// throws <see cref="ArgumentNullException"/> for a null <c>assignee</c>.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Create_NullAssignee_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(
            () => TaskItem.Create("Prepare report", CreateCreator(), null!, null, null));
    }

    /// <summary>
    /// Verifies that <see cref="TaskItem.Create(string, Employee, Employee, DateTimeOffset?, DateTimeOffset?)"/>
    /// throws <see cref="BusinessRuleViolationException"/> with <c>RuleId == "BR5"</c> when the creator and the
    /// assignee are the same employee. Proves BR5: a task cannot be assigned to its creator.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Create_AssigneeIsCreator_ThrowsBusinessRuleViolationBR5()
    {
        var employee = CreateCreator();

        var exception = Assert.Throws<BusinessRuleViolationException>(
            () => TaskItem.Create("Prepare report", employee, employee, null, null));

        Assert.Equal("BR5", exception.RuleId);
    }

    /// <summary>
    /// Verifies that <see cref="TaskItem.Create(string, Employee, Employee, DateTimeOffset?, DateTimeOffset?)"/>
    /// throws <see cref="BusinessRuleViolationException"/> with <c>RuleId == "BR3"</c> when the assignee is
    /// inactive. Proves BR3: an inactive employee cannot be given a new task.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Create_InactiveAssignee_ThrowsBusinessRuleViolationBR3()
    {
        var creator = CreateCreator();
        var assignee = CreateAssignee();
        assignee.Deactivate();

        var exception = Assert.Throws<BusinessRuleViolationException>(
            () => TaskItem.Create("Prepare report", creator, assignee, null, null));

        Assert.Equal("BR3", exception.RuleId);
    }

    /// <summary>
    /// Verifies that <see cref="TaskItem.Create(string, Employee, Employee, DateTimeOffset?, DateTimeOffset?)"/>
    /// succeeds when the creator is inactive and the assignee is active. Proves BR3's scope: only the assignee's
    /// activity is checked, never the creator's.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Create_InactiveCreatorActiveAssignee_Succeeds()
    {
        var creator = CreateCreator();
        creator.Deactivate();
        var assignee = CreateAssignee();

        var task = TaskItem.Create("Prepare report", creator, assignee, null, null);

        Assert.Equal(TaskItemStatus.New, task.Status);
    }

    /// <summary>
    /// Verifies that <see cref="TaskItem.Create(string, Employee, Employee, DateTimeOffset?, DateTimeOffset?)"/>
    /// throws <see cref="BusinessRuleViolationException"/> with <c>RuleId == "BR2"</c> when <c>dueAt</c> is
    /// earlier than <c>plannedStartAt</c>.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Create_DueAtBeforePlannedStartAt_ThrowsBusinessRuleViolationBR2()
    {
        var exception = Assert.Throws<BusinessRuleViolationException>(
            () => TaskItem.Create("Prepare report", CreateCreator(), CreateAssignee(), T0, T0.AddHours(-1)));

        Assert.Equal("BR2", exception.RuleId);
    }

    /// <summary>
    /// Verifies that <see cref="TaskItem.Create(string, Employee, Employee, DateTimeOffset?, DateTimeOffset?)"/>
    /// succeeds when <c>dueAt</c> equals <c>plannedStartAt</c>. BR2 boundary: equal is allowed.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Create_DueAtEqualsPlannedStartAt_Succeeds()
    {
        var task = TaskItem.Create("Prepare report", CreateCreator(), CreateAssignee(), T0, T0);

        Assert.Equal(T0, task.PlannedStartAt);
        Assert.Equal(T0, task.DueAt);
    }

    /// <summary>
    /// Verifies that <see cref="TaskItem.Create(string, Employee, Employee, DateTimeOffset?, DateTimeOffset?)"/>
    /// succeeds when <c>plannedStartAt</c>, <c>dueAt</c>, or both are null. BR2 only applies when both dates are
    /// set.
    /// </summary>
    /// <param name="hasPlanned">Whether <c>plannedStartAt</c> is set.</param>
    /// <param name="hasDue">Whether <c>dueAt</c> is set.</param>
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    [Trait("Category", "Unit")]
    public void Create_PlannedStartAtOrDueAtMissing_Succeeds(bool hasPlanned, bool hasDue)
    {
        DateTimeOffset? plannedStartAt = hasPlanned ? T0 : null;
        DateTimeOffset? dueAt = hasDue ? T0.AddDays(1) : null;

        var task = TaskItem.Create("Prepare report", CreateCreator(), CreateAssignee(), plannedStartAt, dueAt);

        Assert.Equal(plannedStartAt, task.PlannedStartAt);
        Assert.Equal(dueAt, task.DueAt);
    }

    /// <summary>
    /// Verifies that <see cref="TaskItem.Create(string, Employee, Employee, DateTimeOffset?, DateTimeOffset?)"/>
    /// normalises non-UTC offsets on <c>plannedStartAt</c> and <c>dueAt</c> to the same instant with a zero
    /// offset.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Create_NonUtcOffsets_StoresSameInstantWithZeroOffset()
    {
        var plannedStartAt = new DateTimeOffset(2026, 10, 1, 11, 0, 0, TimeSpan.FromHours(2));
        var dueAt = new DateTimeOffset(2026, 10, 1, 5, 0, 0, TimeSpan.FromHours(-5));

        var task = TaskItem.Create("Prepare report", CreateCreator(), CreateAssignee(), plannedStartAt, dueAt);

        Assert.Equal(TimeSpan.Zero, task.PlannedStartAt!.Value.Offset);
        Assert.Equal(plannedStartAt.ToUniversalTime(), task.PlannedStartAt);
        Assert.Equal(TimeSpan.Zero, task.DueAt!.Value.Offset);
        Assert.Equal(dueAt.ToUniversalTime(), task.DueAt);
    }

    /// <summary>
    /// Verifies that <see cref="TaskItem.Create(string, Employee, Employee, DateTimeOffset?, DateTimeOffset?)"/>
    /// throws <see cref="BusinessRuleViolationException"/> with <c>RuleId == "BR2"</c> when <c>dueAt</c> is
    /// earlier than <c>plannedStartAt</c> only after both are normalised to UTC.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Create_DueAtBeforePlannedStartAtAfterUtcConversion_ThrowsBusinessRuleViolationBR2()
    {
        var plannedStartAt = new DateTimeOffset(2026, 10, 1, 10, 0, 0, TimeSpan.Zero);
        var dueAt = new DateTimeOffset(2026, 10, 1, 11, 0, 0, TimeSpan.FromHours(2));

        var exception = Assert.Throws<BusinessRuleViolationException>(
            () => TaskItem.Create("Prepare report", CreateCreator(), CreateAssignee(), plannedStartAt, dueAt));

        Assert.Equal("BR2", exception.RuleId);
    }
}
