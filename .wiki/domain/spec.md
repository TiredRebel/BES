---
okf_version: "0.2"
id: domain-spec
title: Модель даних та контракти (Специфікація)
type: Spec
description: Повна специфікація моделі даних, контрактів, правил BR1-BR5 та сценаріїв використання рівня даних Task Management
status: approved
updated: 2026-09-20
tags: [okf, spec, domain, contracts, efcore, postgresql]
related: ["employee.md", "task-item.md", "br1-completed-at.md", "br2-due-not-before-start.md", "br3-inactive-assignee.md", "br4-final-statuses.md", "br5-no-self-assignment.md", "../decisions/index.md", "../index.md"]
---

# Рівень даних Task Management: Специфікація

[ **Українська** ] · [ English ](spec.en.md)

Контракт, за яким розроблялися модулі A1, A2, B1, B2 та B3. Кожне ім'я у `моноширинному шрифті` є точним символ-у-символ. Усі шляхи коду тут були спроєктовані на етапі N1 і наразі повністю реалізовані (див. [[codemap]]). `[uncertain]` позначає те, що не було підтверджено файлом, документацією чи командою; розділ 16 вказує джерела перевірки.

Модуль: **Task Management**, внутрішній CRM-модуль для призначення, виконання та контролю завдань співробітників.
Його три задачі відображаються на три сценарії використання (use cases):

| Задача | Сценарій використання (§14) | Правила |
|---|---|---|
| Призначення: автор ставить завдання співробітнику | `CreateTaskAsync` | BR2, BR3, BR5 |
| Виконання: завдання рухається життєвим циклом статусів | `ChangeTaskStatusAsync` | BR1, BR4 |
| Контроль: які завдання має співробітник, у якому статусі, з яким дедлайном | `ListTasksByAssigneeAsync` (опціональний фільтр за статусом, сортування за дедлайном) | використовує `ix_tasks_assignee_id_status` |

Область дії охоплює виключно технічне завдання: співробітники, завдання, призначення, дедлайни, статуси, правила BR1–BR5 та три сценарії використання.

---

## 1. Структура рішення

ADR: [0001](../../docs/adr/0001-solution-layout.md).

| Проєкт (шлях) | Вміст | Посилання на проєкти | Пакети NuGet |
|---|---|---|---|
| `src/TaskManagement.Domain/TaskManagement.Domain.csproj` | сутності, переліки, доменний виняток | немає | немає |
| `src/TaskManagement.Infrastructure/TaskManagement.Infrastructure.csproj` | DbContext, Fluent-конфігурації, демо-дані, міграції, фабрика часу проєктування | Domain | `Microsoft.EntityFrameworkCore` 10.0.12; `Microsoft.EntityFrameworkCore.Relational` 10.0.12; `Microsoft.EntityFrameworkCore.Design` 10.0.12; `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.3 |
| `src/TaskManagement.Application/TaskManagement.Application.csproj` | `TaskService` (три сценарії використання) | Domain, Infrastructure | немає (EF Core приходить транзитивно через Infrastructure) |
| `tests/TaskManagement.UnitTests/TaskManagement.UnitTests.csproj` | A2: доменні модульні тести, `[Trait("Category", "Unit")]` | Domain | `Microsoft.NET.Test.Sdk` 17.14.1; `xunit` 2.9.3; `xunit.runner.visualstudio` 3.1.4 |
| `tests/TaskManagement.IntegrationTests/TaskManagement.IntegrationTests.csproj` | B3: тести Testcontainers, `[Trait("Category", "Integration")]` | Domain, Infrastructure, Application | ті самі три, що й у UnitTests + `Testcontainers.PostgreSql` 4.15.0 |

- Файл рішення: `TaskManagement.slnx` у корені репозиторію.
- Обидва тестові проєкти мають `<IsPackable>false</IsPackable>` та `<Using Include="Xunit" />`.
- **Два тестові проєкти, а не один**: модульний проєкт посилається тільки на `Domain`, тому `dotnet test --filter Category=Unit` не потребує ні EF Core, ні Docker.

---

## 2. Пастки іменування (Naming traps)

Оскільки `ImplicitUsings` імпортує `System.Threading.Tasks`:
- Сутність завдання називається **`TaskItem`**, ніколи `Task`.
- Перелік статусів називається **`TaskItemStatus`**, ніколи `TaskStatus`.
- Жоден тип, простір імен чи файл не має назви `Task`, `Tasks` або `TaskStatus`. Єдине, що зветься `Tasks`, — це властивість `DbSet<TaskItem>` у `DbContext`.
- Сама таблиця в базі даних називається `tasks`.
- Правила аналізаторів:
  - CA1510: `ArgumentNullException.ThrowIfNull(x)` замість ручної перевірки.
  - CA2208: використання коректних імен параметрів у винятках.
  - CA2201: ніколи не викидати `new Exception(...)`.
  - CA1710/CA1711: типи винятків закінчуються на `Exception`.
  - `EF Core 10`: `ToTable("tasks", t => t.HasCheckConstraint(name, sql))` замість застарілого `HasCheckConstraint` на білдері сутності.

---

## 2a. Стандарт документації

- Кожен публічний тип та член повинен мати XML-документацію: `<summary>`, `<param>`, `<returns>`, `<exception>`.
- Там, де код забезпечує бізнес-правило, тег `<remarks>` вказує його (наприклад, `Enforces BR5: ...`).
- Кожен клас Fluent-конфігурації документує у своїх XML-коментарях, які індекси та обмеження він створює і чому.
- Міграція `InitialCreate` починається із заголовного коментаря зі списком усіх змін схеми.

---

## 3. Сутності (`TaskManagement.Domain`)

Без атрибутів Data Annotations. Без навігаційних властивостей: зовнішні ключі є звичайними властивостями `Guid`. Усі властивості мають `private set`. Приватний конструктор без параметрів для EF Core.

### 3.1 `Employee` (`public sealed class Employee`)

| Властивість / Член | Тип | Примітки |
|---|---|---|
| `FullNameMaxLength = 200;` | `int const` | спільна константа з B1 (`HasMaxLength`) |
| `EmailMaxLength = 254;` | `int const` | спільна константа з B1 |
| `Id` | `Guid` | генерується через `Guid.CreateVersion7()` |
| `FullName` | `string` | обрізається, 1..200 символів |
| `Email` | `string` | обрізається, переводиться в нижній регістр (`ToLowerInvariant()`), 1..254 символів |
| `IsActive` | `bool` | `true` при створенні; `false` після `Deactivate()` (BR3) |

- `public static Employee Create(string fullName, string email)`: валідує параметри, повертає активного співробітника з новим `Id`.
- `public void Deactivate()`: встановлює `IsActive = false`. Ідемпотентний.

### 3.2 `TaskItem` (`public sealed class TaskItem`)

| Властивість / Член | Тип | Примітки |
|---|---|---|
| `TitleMaxLength = 200;` | `int const` | константа довжини заголовка |
| `Id` | `Guid` | генерується через `Guid.CreateVersion7()` |
| `Title` | `string` | обрізається, 1..200 символів |
| `Status` | `TaskItemStatus` | `New` після `Create` |
| `CreatorId` | `Guid` | ідентифікатор автора; ніколи не змінюється |
| `AssigneeId` | `Guid` | ідентифікатор виконавця; ніколи не змінюється (без перепризначень, ADR 0002) |
| `PlannedStartAt` | `DateTimeOffset?` | UTC або null |
| `DueAt` | `DateTimeOffset?` | UTC або null; дедлайн |
| `CompletedAt` | `DateTimeOffset?` | UTC; заповнено тоді й тільки тоді, коли `Status == Completed` (BR1) |
| `Version` | `uint` | токен конкурентності, зіставлений із `xmin` у PostgreSQL |

- `public static TaskItem Create(...)`:
  Порядок перевірок:
  1. `ArgumentException.ThrowIfNullOrWhiteSpace(title)`.
  2. Довжина заголовка > 200 → `ArgumentException`.
  3. `ArgumentNullException.ThrowIfNull(creator)`, `ArgumentNullException.ThrowIfNull(assignee)`.
  4. Нормалізація обох дат через `ToUniversalTime()`.
  5. **BR5**: `creator.Id == assignee.Id` → виняток `BusinessRuleViolationException("BR5")`.
  6. **BR3**: `!assignee.IsActive` → виняток `BusinessRuleViolationException("BR3")`.
  7. **BR2**: якщо обидві дати задані та `dueAt < plannedStartAt` → виняток `BusinessRuleViolationException("BR2")`.
  8. Повертає завдання зі статусом `New`, `CompletedAt = null`.

- `public void ChangeStatus(TaskItemStatus newStatus, DateTimeOffset changedAt)`:
  1. `!Enum.IsDefined(newStatus)` → `ArgumentOutOfRangeException`.
  2. **BR4**: поточний `Status` є `Completed` або `Cancelled` → виняток `BusinessRuleViolationException("BR4")`.
  3. `newStatus == Status` → return (no-op).
  4. `Status = newStatus; CompletedAt = newStatus == TaskItemStatus.Completed ? changedAt.ToUniversalTime() : null;` (**BR1**).

### 3.3 `TaskItemStatus` (`public enum TaskItemStatus`)

```csharp
New = 0, InProgress = 1, Completed = 2, Cancelled = 3
```

### 3.4 `BusinessRuleViolationException`

```csharp
public sealed class BusinessRuleViolationException : InvalidOperationException
{
    public BusinessRuleViolationException(string ruleId, string message);
    public BusinessRuleViolationException(string ruleId, string message, Exception innerException);
    public string RuleId { get; } // "BR2" | "BR3" | "BR4" | "BR5"
}
```

---

## 4. Модель призначення (Assignment model)

Виконавець — це обов'язкова колонка `assignee_id` у таблиці `tasks`, яка встановлюється один раз при виклику `TaskItem.Create`. Окрема таблиця історії призначень відсутня. Це дозволяє забезпечити BR5 за допомогою локального обмеження `CHECK (assignee_id <> creator_id)` (ADR 0002).

---

## 5. Статуси та матриця переходів

Статуси зберігаються як текст: `varchar(20) NOT NULL`, обмежені `ck_tasks_status_valid` (ADR 0007).
Правило переходу: зміна дозволена тоді й лише тоді, коли поточний статус не є фінальним.

| З (From) | До (To) | Дозволено? | Результат | `CompletedAt` після зміни |
|---|---|:---:|---|---|
| New | New | так | no-op | null |
| New | InProgress | так | status = InProgress | null |
| New | Completed | так | status = Completed | changedAt (UTC) |
| New | Cancelled | так | status = Cancelled | null |
| InProgress | New | так | status = New | null |
| InProgress | InProgress | так | no-op | null |
| InProgress | Completed | так | status = Completed | changedAt (UTC) |
| InProgress | Cancelled | так | status = Cancelled | null |
| Completed | Будь-який | **ні** | BR4 exception, без змін | без змін (non-null) |
| Cancelled | Будь-який | **ні** | BR4 exception, без змін | null |

---

## 6. Час (Time)

ADR: [0006](../../docs/adr/0006-utc-normalisation-and-explicit-time.md).
- Усі мітки часу є `DateTimeOffset`, що зберігаються як `timestamp with time zone` (UTC).
- Домен нормалізує кожну дату за допомогою `ToUniversalTime()`.
- Домен не звертається до годинника напряму: `ChangeStatus` приймає `changedAt` явно; `TaskService` отримує `TimeProvider` через DI-конструктор.

---

## 7. Дедлайни та BR2

`PlannedStartAt` та `DueAt` є nullable. BR2 застосовується лише тоді, коли обидві дати задані: `DueAt >= PlannedStartAt`. Якщо хоча б одна дата відсутня (`null`), BR2 вважається виконаним.

---

## 8. BR3: «призначення нового завдання»

Співробітнику призначається нове завдання тільки в момент створення `TaskItem`. BR3 перевіряється тричі:
1. **Application** (`TaskService.CreateTaskAsync`): перевірка `!assignee.IsActive` після завантаження з БД.
2. **Domain** (`TaskItem.Create` крок 6): перевірка об'єкта `assignee.IsActive`.
3. **Database** (тригер `trg_tasks_br3_br4`): читання рядка виконавця під блокуванням `FOR SHARE` під час `INSERT`.

---

## 9. Первинні ключі

Ключі `Guid` генеруються доменом за стандартом UUIDv7 (`Guid.CreateVersion7()`), зіставлені з типом `uuid` і мають `ValueGeneratedNever()` (ADR 0003).

---

## 10. Реляційний мапінг (B1)

Кожна властивість має явний виклик `HasColumnName`:
- `employees`: `id`, `full_name`, `email`, `is_active`. Таблиця: `employees`.
- `tasks`: `id`, `title`, `status`, `creator_id`, `assignee_id`, `planned_start_at`, `due_at`, `completed_at`, `xmin` (версія).
- Зовнішні ключі налаштовані з `DeleteBehavior.Restrict` (`ON DELETE RESTRICT`).

---

## 11. Індекси та обмеження

| Ім'я | Таблиця | Тип | Колонки / SQL | Призначення |
|---|---|---|---|---|
| `pk_employees` | employees | PK | `(id)` | первинний ключ |
| `pk_tasks` | tasks | PK | `(id)` | первинний ключ |
| `ux_employees_email` | employees | Unique index | `(email)` | унікальність e-mail |
| `ix_tasks_assignee_id_status` | tasks | Index | `(assignee_id, status)` | пошук за виконавцем та статусом |
| `ix_tasks_due_at` | tasks | Index | `(due_at)` | сортування та пошук за дедлайном |
| `ix_tasks_creator_id` | tasks | Index | `(creator_id)` | оптимізація FK автора |
| `fk_tasks_employees_creator_id` | tasks | FK | `(creator_id) → employees(id) ON DELETE RESTRICT` | зв'язок з автором |
| `fk_tasks_employees_assignee_id` | tasks | FK | `(assignee_id) → employees(id) ON DELETE RESTRICT` | зв'язок з виконавцем |
| `ck_tasks_status_valid` | tasks | CHECK | `status IN ('New', 'InProgress', 'Completed', 'Cancelled')` | валідність переліку |
| `ck_tasks_br1_completed_at_iff_completed` | tasks | CHECK | `(status = 'Completed' AND completed_at IS NOT NULL) OR (status <> 'Completed' AND completed_at IS NULL)` | **BR1** |
| `ck_tasks_br2_due_at_not_before_planned_start_at` | tasks | CHECK | `planned_start_at IS NULL OR due_at IS NULL OR due_at >= planned_start_at` | **BR2** |
| `ck_tasks_br5_assignee_not_creator` | tasks | CHECK | `assignee_id <> creator_id` | **BR5** |
| `trg_tasks_br3_br4` | tasks | Trigger | `BEFORE INSERT OR UPDATE OF status FOR EACH ROW` | **BR3, BR4** |

SQL тригера:
```sql
CREATE FUNCTION tasks_enforce_br3_br4() RETURNS trigger
LANGUAGE plpgsql AS $$
DECLARE
    assignee_active boolean;
BEGIN
    IF TG_OP = 'INSERT' THEN
        SELECT is_active INTO assignee_active FROM employees WHERE id = NEW.assignee_id FOR SHARE;
        IF assignee_active IS FALSE THEN
            RAISE EXCEPTION 'BR3: employee % is inactive and cannot be given a new task.', NEW.assignee_id
                USING ERRCODE = 'check_violation', CONSTRAINT = 'trg_tasks_br3_assignee_active';
        END IF;
    ELSIF OLD.status IN ('Completed', 'Cancelled') AND NEW.status IS DISTINCT FROM OLD.status THEN
        RAISE EXCEPTION 'BR4: task % is %; Completed and Cancelled are final.', OLD.id, OLD.status
            USING ERRCODE = 'check_violation', CONSTRAINT = 'trg_tasks_br4_final_status';
    END IF;
    RETURN NEW;
END;
$$;

CREATE TRIGGER trg_tasks_br3_br4
    BEFORE INSERT OR UPDATE OF status ON tasks
    FOR EACH ROW EXECUTE FUNCTION tasks_enforce_br3_br4();
```

---

## 12. Початкові демо-дані (Seed data)

Налаштовані через `HasData` в класах конфігурації:
- Співробітники: Олена Коваленко (`...0001`, active), Богдан Шевченко (`...0002`, active), Оксана Мельник (`...0003`, inactive).
- Завдання:
  - `...0001`: "Підготувати квартальний звіт з продажів" (`New`, Олена → Богдан).
  - `...0002`: "Передзвонити ключовому клієнту" (`Completed`, Богдан → Олена, `CompletedAt` задано).
  - `...0003`: "Очистити дублікати контактів" (`Cancelled`, Олена → Богдан).

---

## 13. Контракт DbContext (`TaskManagement.Infrastructure`)

- `public sealed class TaskManagementDbContext : DbContext`
- `public DbSet<Employee> Employees => Set<Employee>();`
- `public DbSet<TaskItem> Tasks => Set<TaskItem>();`
- Застосування конфігурацій: `modelBuilder.ApplyConfigurationsFromAssembly(...)`.
- Фабрика часу проєктування: `TaskManagementDbContextFactory : IDesignTimeDbContextFactory<TaskManagementDbContext>`.

---

## 14. Контракт прикладного сервісу (`TaskManagement.Application`)

```csharp
public sealed record TaskCursor(DateTimeOffset? DueAt, Guid Id);

public sealed class PagedResult<T> : IReadOnlyList<T>
{
    public IReadOnlyList<T> Items { get; }
    public TaskCursor? NextCursor { get; }
    public bool HasNextPage => NextCursor is not null;
    public int Count => Items.Count;
    public T this[int index] => Items[index];
    public IEnumerator<T> GetEnumerator() => Items.GetEnumerator();
}

public sealed record TaskListQuery(
    Guid AssigneeId,
    TaskItemStatus? Status = null,
    DateTimeOffset? DueFrom = null,
    DateTimeOffset? DueTo = null,
    Guid? CreatorId = null,
    int PageSize = TaskListQuery.DefaultPageSize,
    TaskCursor? Cursor = null)
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 100;
    public const int MinPageSize = 1;
}

public sealed class TaskService
{
    public TaskService(TaskManagementDbContext dbContext, TimeProvider timeProvider);

    public Task<TaskItem> CreateTaskAsync(
        string title, Guid creatorId, Guid assigneeId,
        DateTimeOffset? plannedStartAt, DateTimeOffset? dueAt,
        CancellationToken cancellationToken = default);

    public Task<TaskItem> ChangeTaskStatusAsync(
        Guid taskId, TaskItemStatus newStatus,
        CancellationToken cancellationToken = default);

    public Task<PagedResult<TaskItem>> ListTasksAsync(
        TaskListQuery query,
        CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<TaskItem>> ListTasksByAssigneeAsync(
        Guid assigneeId, TaskItemStatus? status = null,
        CancellationToken cancellationToken = default);
}
```

- **`CreateTaskAsync`**: перевіряє існування авторів, активність виконавця (BR3), валідує в домені, зберігає в БД; помилку тригера `trg_tasks_br3_assignee_active` транслює в `BusinessRuleViolationException("BR3", ..., inner)`.
- **`ChangeTaskStatusAsync`**: завантажує завдання, викликає `task.ChangeStatus(newStatus, timeProvider.GetUtcNow())`, зберігає зміни. При конфлікті викидає `DbUpdateConcurrencyException`.
- **`ListTasksAsync`**: виконує `AsNoTracking()` запит за `query.AssigneeId`, обмежує розмір сторінки `Math.Clamp(query.PageSize, 1, 100)`, застосовує keyset-фільтр за курсором `(DueAt, Id)` та опціональні фільтри (`Status`, `CreatorId`, `DueFrom`, `DueTo`), впорядковує за `DueAt` (nulls last) та `Id`, повертає сторінку `PagedResult<TaskItem>` з курсором `NextCursor`.
- **`ListTasksByAssigneeAsync`**: сумісна обгортка, яка делегує виклик у `ListTasksAsync(new TaskListQuery(assigneeId, status), cancellationToken)`.

---

## 15. План тестування (Test Plan)

- **Модульні тести (A2)**: `EmployeeTests`, `TaskItemCreateTests`, `TaskItemChangeStatusTests`. Покривають усі валідації, межі довжини рядків, порядок guard-перевірок, нормалізацію дат до UTC та повну таблицю з 16 переходів між статусами.
- **Інтеграційні тести (B3)**: `MigrationAndSeedTests`, `DatabaseConstraintTests`, `TaskServiceTests`. Запускаються проти реального PostgreSQL 17 через Testcontainers. Перевіряють застосування міграції, завантаження seed-даних, реакцію БД на некоректні SQL-запити (перевірка SqlState `23514`, `23503`, `23505`) та всі сценарії сервісу.

---

## 16. Верифікація (Verification)

Усі версії пакетів, API та методи перевірені у відповідних файлах XML-документації SDK, NuGet та тестах:
- .NET 10.0.401, C# 13 / 14, EF Core 10.0.12, Npgsql 10.0.3, Testcontainers 4.15.0, xunit 2.9.3.
- Поведінка PostgreSQL перевірена на тестовому контейнері `postgres:17-alpine`.
