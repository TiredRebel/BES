---
okf_version: "0.2"
id: task-management-knowledge-index
title: База знань (OKF Index)
type: Index
description: Компактна семантична пам'ять та карта прийняття рішень для AI-агентів у форматі Open Knowledge Format (OKF v0.2)
status: active
updated: 2026-09-20
tags: [okf, knowledge-bundle, crm, data-layer, dotnet, postgresql]
related: ["codemap.md", "domain/spec.md", "decisions/index.md", "log.md"]
---

# Рівень даних Task Management: База знань (OKF Index)

[ **Українська** ] · [ English ](index.en.md)

**Task Management** - це внутрішній CRM-модуль для призначення, виконання та контролю завдань співробітників. Цей репозиторій є його рівнем даних (PostgreSQL + EF Core).

Цей каталог є офіційним **Open Knowledge Format (OKF v0.2)** бандлом знань проєкту. Він слугує компактною семантичною пам'яттю для автономних AI-агентів (Claude Code, Antigravity, Codex, Cursor, AWS Bedrock) та розробників. Інструкції для агентів знаходяться в `AGENTS.md`. Формат - чистий Markdown із типізованим YAML frontmatter та відносними посиланнями, повністю сумісний із переглядачами OKF Reader / OKF Workbench та Obsidian.

## Матриця пошуку та прийняття рішень (OKF Retrieval Map)

Для швидкого прийняття рішень без зайвого пошуку агенти звертаються безпосередньо до відповідних OKF-концептів:

| Потреба / Задача агента | Рекомендований документ для читання |
|---|---|
| **Швидкий старт / Відновлення контексту** | Найновіший чекпоінт у [checkpoints/](checkpoints/) та останні записи в [log.md](log.md) |
| **Синтаксичний граф коду, AST та залежності** | [codemap.md](codemap.md) (матеріалізований граф CodeGraph) |
| **Повна специфікація моделі даних та контрактів** | [domain/spec.md](domain/spec.md) (специфікація N1) |
| **Сутності домену** | [domain/employee.md](domain/employee.md), [domain/task-item.md](domain/task-item.md) |
| **Бізнес-правила (BR1-BR5)** | [BR1](domain/br1-completed-at.md), [BR2](domain/br2-due-not-before-start.md), [BR3](domain/br3-inactive-assignee.md), [BR4](domain/br4-final-statuses.md), [BR5](domain/br5-no-self-assignment.md) |
| **Архітектурні рішення (ADR)** | [decisions/index.md](decisions/index.md) (ADR 0001-0009) |
| **Граф виконання завдань (DAG)** | [plan/graph.yaml](plan/graph.yaml) |

## Поточний стан (оновлено 2026-09-19)

- **Усі вузли графа виконання та архітектурні вдосконалення завершено.** Гілка `feature/task-management`.
  `dotnet build -warnaserror` 0/0; `dotnet test` 101/101 (56 модульних, 45 інтеграційних на PostgreSQL 17 через Testcontainers).
- **Реалізовані покращення (Сесія 2):**
  1. **Сід-дані українською мовою:** "Олена Коваленко", "Богдан Шевченко", "Оксана Мельник" та україномовні завдання; перевірено валідацію рядків та поведінку PostgreSQL UTF-8.
  2. **Рекомендація 1.1 (Query Object):** `TaskListQuery` та метод `ListTasksAsync` з підтримкою фільтрації за `AssigneeId`, `Status`, `DueFrom`, `DueTo`, `CreatorId`, `PageSize`, `Cursor`. Метод `ListTasksByAssigneeAsync` збережено як зворотно-сумісну обгортку.
  3. **Рекомендація 2.1 (Keyset пагінація):** реалізовано курсорну пагінацію `TaskCursor(DueAt, Id)` та `PagedResult<T> : IReadOnlyList<T>` із сортуванням `due_at ASC NULLS LAST, id ASC` та обмеженням розміру сторінки $1 \le \text{PageSize} \le 100$.
  4. **State Pattern / FSM:** переходи статусів переведено на незмінну матрицю `FrozenDictionary<TaskItemStatus, FrozenSet<TaskItemStatus>> AllowedTransitions` в `TaskItem`.
  5. **Архітектурний опис:** додано детальний звіт та технічне обґрунтування рішень у `README.md` (розділи 2 та 3).
  6. **Двомовна документація:** додано повний переклад українською/англійською для всіх документів (`README.md` / `README.en.md`, `.wiki/index.md` / `index.en.md`, `spec.md` / `spec.en.md`, сторінки BR1–BR5) із взаємними перемикачами мов та клікабельними посиланнями.
- **Точка відновлення:** найновіший чекпоінт у `checkpoints/`, потім два останні записи в [[log]], потім `agents/bus/orch-to-D4-001.md`.
- **Очікують рішення людини (3 питання):**
  1. Затвердити правки D4 до узгодженої специфікації: §14 (очищення після невдалого збереження), §15 (нові тести), сторінки BR4/BR5. Розділи схеми §10–§12 не змінювалися.
  2. Розширити тригер для перевірки BR3 також при перепризначенні (`UPDATE OF status, assignee_id` + перевірка BR3 на таких оновленнях)? Зміна схеми (ADR 0009).
  3. Обмежити трансляцію помилки BR3 тільки завданням поточного виклику (висновок рев'ю SF3), чи зберегти поточну поведінку?
- **Закрито документацією, а не кодом:** SF3 (див. рішення 3), N1/SF6 (після невдалого збереження завдання від'єднується, тому стратегія EF "client wins" нічого не зберігає: повторіть виклик через сервіс), F2/T1 (див. рішення 2).
- **Залишається зі статусом `[uncertain]`:** Antigravity читає кореневий `AGENTS.md` та його конфіг MCP; хук чекпоінтів запускався лише вручну; повідомлення "error using the connection" при першому `database update` (exit 0); токен `xmin` не можна вилучити ізольовано; причина `pg_constraint` у PostgreSQL 18 у тесті схеми; обрізання не-ASCII символів у нижній регістр в ADR 0005.
- **Наступні кроки:** видалити 5 завершених воркстрі під `.claude/worktrees/` та їхні гілки `worktree-agent-*` (потрібна згода користувача); опціональне посилення тестів (таймінг `FOR SHARE`, скасування під час збереження, оновлення того самого статусу, дозволене тригером) та пропущені дрібні зауваження в `agents/bus/orch-to-D4-001.md`.
- **Історія:** користувач затвердив специфікацію на етапі N2 та ще чотири рішення під час попереднього обговорення (тригер BR3/BR4, фільтр статусу, демо-дані в `InitialCreate`, трансляція помилки тригера BR3 в доменний виняток); див. [[log]].

## Сторінки бази знань

| Сторінка | Вміст |
|---|---|
| [[log]] | Лог сесій та комітів (append-only). Найновіші записи показують поточний стан. |
| `plan/graph.yaml` | Граф виконання (DAG): рівень кожного вузла, вхідні/вихідні дані, команди перевірки та статус. |
| [[codemap]] | Карта коду, побудована з індексу CodeGraph: проєкти → простори імен → типи → залежності. |
| [[spec]] | Модель даних та контракти: сутності, статуси і переходи, час, ключі, мапінг, обмеження, seed, DbContext, сервіс, план тестів. |
| [[employee]] | Сторінка сутності `Employee`. |
| [[task-item]] | Сторінка сутності `TaskItem` та переліку `TaskItemStatus`. |
| [[br1-completed-at]] | BR1: `CompletedAt` встановлюється тоді й тільки тоді, коли статус = `Completed`. |
| [[br2-due-not-before-start]] | BR2: `DueAt` не може бути раніше за `PlannedStartAt`. |
| [[br3-inactive-assignee]] | BR3: неактивному співробітнику не можна призначити нове завдання. |
| [[br4-final-statuses]] | BR4: `Completed` та `Cancelled` є фінальними. |
| [[br5-no-self-assignment]] | BR5: виконавець ≠ автор завдання. |
| [[decisions/index]] | Посилання на архітектурні рішення ADR у `docs/adr/` (0001–0009; 0009 замінює 0004). |
| `checkpoints/` | Автоматичні чекпоінти прогресу, що записуються хуками Claude Code та агентами. |
| `agents/bus/` | Шина повідомлень агентів: один файл на повідомлення, `<від>-to-<кому>-<номер>.md`. |

## Структура рішення (затверджено на N2; див. [[spec]] §1)

`src/TaskManagement.Domain`, `src/TaskManagement.Infrastructure`, `src/TaskManagement.Application`,
`tests/TaskManagement.UnitTests`, `tests/TaskManagement.IntegrationTests`, файл рішення `TaskManagement.slnx`.

## Бізнес-правила

- **BR1** `CompletedAt` обов'язковий тоді й тільки тоді, коли статус = `Completed`.
- **BR2** `DueAt` не може бути раніше за `PlannedStartAt`.
- **BR3** Неактивному співробітнику не можна призначити нове завдання.
- **BR4** `Completed` та `Cancelled` є фінальними. Перехід з них неможливий.
- **BR5** Завдання не може бути призначене своєму автору (виконавець ≠ автор).
