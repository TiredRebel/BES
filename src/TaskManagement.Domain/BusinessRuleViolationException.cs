namespace TaskManagement.Domain;

/// <summary>
/// Thrown when an operation would violate one of the module's business rules (BR2–BR5). BR1 is a structural
/// invariant that the domain cannot violate through its public API, so it never throws this exception.
/// </summary>
/// <remarks>
/// Input errors (null, blank or too long) stay <see cref="ArgumentNullException"/>, <see cref="ArgumentException"/>
/// or <see cref="ArgumentOutOfRangeException"/>. Missing rows are <see cref="KeyNotFoundException"/> (service only).
/// </remarks>
public sealed class BusinessRuleViolationException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BusinessRuleViolationException"/> class.
    /// </summary>
    /// <param name="ruleId">The violated business rule's identifier ("BR2" | "BR3" | "BR4" | "BR5").</param>
    /// <param name="message">A message describing the violation.</param>
    public BusinessRuleViolationException(string ruleId, string message)
        : base(message)
    {
        RuleId = ruleId;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BusinessRuleViolationException"/> class, wrapping the
    /// exception that revealed the violation.
    /// </summary>
    /// <param name="ruleId">The violated business rule's identifier ("BR2" | "BR3" | "BR4" | "BR5").</param>
    /// <param name="message">A message describing the violation.</param>
    /// <param name="innerException">The exception that revealed the violation.</param>
    /// <remarks>
    /// Used by <c>TaskService.CreateTaskAsync</c> to turn the database trigger's BR3 rejection into this
    /// exception while keeping the original as <see cref="Exception.InnerException"/>.
    /// </remarks>
    public BusinessRuleViolationException(string ruleId, string message, Exception innerException)
        : base(message, innerException)
    {
        RuleId = ruleId;
    }

    /// <summary>
    /// Gets the violated business rule's identifier ("BR2" | "BR3" | "BR4" | "BR5").
    /// </summary>
    public string RuleId { get; }
}
