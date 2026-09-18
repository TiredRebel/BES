# 0008. One domain exception for business-rule violations

Status: proposed (N1, 2026-09-18). Awaiting the N2 human gate.

## Context

Guards in `TaskItem.Create` and `TaskItem.ChangeStatus` reject both plain bad input (blank title, null employee)
and business-rule violations (BR2, BR3, BR4, BR5). With BCL exceptions only, BR2/BR5 and a blank title would all be
`ArgumentException`, and a caller or a test could not tell which rule fired. A2 writes its tests from the spec
without seeing A1's code, and the fan-in's red/green evidence needs each test to fail only when its own rule's guard
is disabled.

## Decision

- `public sealed class BusinessRuleViolationException : InvalidOperationException` in `TaskManagement.Domain`, with
  one constructor `(string ruleId, string message)` and a `public string RuleId { get; }` (`"BR2"`–`"BR5"`).
- Thrown for BR2, BR3, BR4, BR5 (domain) and for BR3 in the service. BR1 cannot be violated through the API.
- Input validation keeps BCL types (`ArgumentNullException`, `ArgumentException`, `ArgumentOutOfRangeException`);
  missing rows in the service are `KeyNotFoundException`.
- Tests assert `RuleId`, never message text.
- CA1032 (standard exception constructors) is not enabled under `latest-recommended`, so the single constructor
  builds.

## Consequences

- Callers can catch one type for "the request breaks a business rule" and still catch `InvalidOperationException`
  generally.
- Rule ids are string literals in two places (domain and service). Acceptable for four values; a constants class is
  not worth a file.
