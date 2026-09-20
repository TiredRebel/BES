---
okf_version: "0.2"
id: adr-index
title: Архітектурні рішення (ADR)
type: Index
description: Реєстр та індекс архітектурних рішень (ADR 0001-0009) рівня даних Task Management
status: approved
updated: 2026-09-20
tags: [okf, adr, architecture-decisions, index]
related: ["../domain/spec.md", "../index.md"]
---

# Архітектурні рішення (Architecture decisions)

[ **Українська** ] · [ English ](index.en.md)

Один рядок на кожен ADR. Самі звіти ADR знаходяться в директорії `docs/adr/`. Рішення 0001–0008 були затверджені на етапі N2; 0009 з'явився під час попереднього обговорення реалізації та замінює 0004.

- [0001 Структура рішення: три вихідні проєкти, два тестові проєкти](../../docs/adr/0001-solution-layout.md)
- [0002 Призначення є колонкою завдання, а не окремою таблицею історії](../../docs/adr/0002-assignee-column-on-task.md)
- [0003 Первинні ключі: UUIDv7, які генеруються доменом](../../docs/adr/0003-domain-generated-uuid-keys.md)
- [0004 BR3 та BR4 не мають обмежень БД (плюс токен конкурентності xmin)](../../docs/adr/0004-no-db-constraint-for-br3-br4.md), замінено на 0009 за винятком токена xmin
- [0005 Унікальність e-mail є нечутливою до регістру через нормалізацію до нижнього регістру](../../docs/adr/0005-email-uniqueness-lowercase.md)
- [0006 Мітки часу: нормалізація до UTC у домені; час надходить із TimeProvider](../../docs/adr/0006-utc-normalisation-and-explicit-time.md)
- [0007 Статус завдання: чотири значення, збереження у вигляді тексту, одне правило переходів](../../docs/adr/0007-status-as-text-and-transition-rule.md)
- [0008 Єдиний доменний виняток для порушень бізнес-правил](../../docs/adr/0008-business-rule-violation-exception.md) (доповнено: конструктор із внутрішнім винятком)
- [0009 BR3 та BR4 забезпечуються в базі даних через тригер](../../docs/adr/0009-br3-br4-enforced-by-trigger.md)
