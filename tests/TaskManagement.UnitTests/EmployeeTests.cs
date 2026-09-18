using TaskManagement.Domain;

namespace TaskManagement.UnitTests;

/// <summary>
/// Unit tests for <see cref="Employee.Create(string, string)"/> and <see cref="Employee.Deactivate"/>.
/// </summary>
public sealed class EmployeeTests
{
    /// <summary>
    /// Verifies that <see cref="Employee.Create(string, string)"/> sets <c>FullName</c>, <c>Email</c>, assigns a
    /// non-empty <c>Id</c> and starts the employee active.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Create_ValidInput_SetsPropertiesAndIsActive()
    {
        var employee = Employee.Create("Alice Morgan", "alice.morgan@example.com");

        Assert.Equal("Alice Morgan", employee.FullName);
        Assert.Equal("alice.morgan@example.com", employee.Email);
        Assert.True(employee.IsActive);
        Assert.NotEqual(Guid.Empty, employee.Id);
    }

    /// <summary>
    /// Verifies that <see cref="Employee.Create(string, string)"/> trims and lowercases the e-mail address.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Create_MixedCaseEmailWithSpaces_StoresTrimmedLowercase()
    {
        var employee = Employee.Create("Alice Morgan", "  Alice.Morgan@Example.COM ");

        Assert.Equal("alice.morgan@example.com", employee.Email);
    }

    /// <summary>
    /// Verifies that <see cref="Employee.Create(string, string)"/> throws <see cref="ArgumentNullException"/> for
    /// a null <c>fullName</c>.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Create_NullFullName_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => Employee.Create(null!, "alice.morgan@example.com"));
    }

    /// <summary>
    /// Verifies that <see cref="Employee.Create(string, string)"/> throws <see cref="ArgumentException"/> for an
    /// empty or whitespace-only <c>fullName</c>.
    /// </summary>
    /// <param name="fullName">The blank full name under test.</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [Trait("Category", "Unit")]
    public void Create_BlankFullName_ThrowsArgumentException(string fullName)
    {
        Assert.Throws<ArgumentException>(() => Employee.Create(fullName, "alice.morgan@example.com"));
    }

    /// <summary>
    /// Verifies that <see cref="Employee.Create(string, string)"/> accepts a <c>fullName</c> at the 200-character
    /// boundary.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Create_FullNameOf200Chars_Succeeds()
    {
        var fullName = new string('a', Employee.FullNameMaxLength);

        var employee = Employee.Create(fullName, "alice.morgan@example.com");

        Assert.Equal(fullName, employee.FullName);
    }

    /// <summary>
    /// Verifies that <see cref="Employee.Create(string, string)"/> throws <see cref="ArgumentException"/> when
    /// <c>fullName</c> is one character past the 200-character boundary.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Create_FullNameOf201Chars_ThrowsArgumentException()
    {
        var fullName = new string('a', Employee.FullNameMaxLength + 1);

        Assert.Throws<ArgumentException>(() => Employee.Create(fullName, "alice.morgan@example.com"));
    }

    /// <summary>
    /// Verifies that <see cref="Employee.Create(string, string)"/> throws <see cref="ArgumentNullException"/> for
    /// a null <c>email</c>.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Create_NullEmail_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => Employee.Create("Alice Morgan", null!));
    }

    /// <summary>
    /// Verifies that <see cref="Employee.Create(string, string)"/> throws <see cref="ArgumentException"/> for an
    /// empty or whitespace-only <c>email</c>.
    /// </summary>
    /// <param name="email">The blank e-mail address under test.</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [Trait("Category", "Unit")]
    public void Create_BlankEmail_ThrowsArgumentException(string email)
    {
        Assert.Throws<ArgumentException>(() => Employee.Create("Alice Morgan", email));
    }

    /// <summary>
    /// Verifies that <see cref="Employee.Create(string, string)"/> accepts an <c>email</c> at the 254-character
    /// boundary.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Create_EmailOf254Chars_Succeeds()
    {
        var email = new string('a', 242) + "@example.com";
        Assert.Equal(Employee.EmailMaxLength, email.Length);

        var employee = Employee.Create("Alice Morgan", email);

        Assert.Equal(email, employee.Email);
    }

    /// <summary>
    /// Verifies that <see cref="Employee.Create(string, string)"/> throws <see cref="ArgumentException"/> when
    /// <c>email</c> is one character past the 254-character boundary.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Create_EmailOf255Chars_ThrowsArgumentException()
    {
        var email = new string('a', 243) + "@example.com";

        Assert.Throws<ArgumentException>(() => Employee.Create("Alice Morgan", email));
    }

    /// <summary>
    /// Verifies that two calls to <see cref="Employee.Create(string, string)"/> produce distinct <c>Id</c> values.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Create_TwoCalls_ReturnDistinctIds()
    {
        var first = Employee.Create("Alice Morgan", "alice.morgan@example.com");
        var second = Employee.Create("Bob Chen", "bob.chen@example.com");

        Assert.NotEqual(first.Id, second.Id);
    }

    /// <summary>
    /// Verifies that <see cref="Employee.Deactivate"/> sets <c>IsActive</c> to <see langword="false"/>. This is
    /// the precondition BR3 relies on: an inactive employee cannot be given a new task.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Deactivate_ActiveEmployee_SetsIsActiveFalse()
    {
        var employee = Employee.Create("Alice Morgan", "alice.morgan@example.com");

        employee.Deactivate();

        Assert.False(employee.IsActive);
    }

    /// <summary>
    /// Verifies that <see cref="Employee.Deactivate"/> is idempotent: calling it on an already inactive employee
    /// does not throw and leaves <c>IsActive</c> false.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Deactivate_InactiveEmployee_StaysInactive()
    {
        var employee = Employee.Create("Alice Morgan", "alice.morgan@example.com");
        employee.Deactivate();

        employee.Deactivate();

        Assert.False(employee.IsActive);
    }
}
