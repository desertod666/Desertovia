> Обязательная исходная основа: GOAL_1_0.md и source-package/04_ROADMAP.txt / 05_TASKS.json / DEPENDENCY_LAYERS.json.
> Текущая сверка и следующий допустимый шаг — EXECUTION_SEQUENCE_1_0.md; таблица ниже не является отчётом о полном закрытии этапов.

> **D-106:** [игровая концепция фермы](FARM_GAMEPLAY_CONCEPT_1_0.md) включена
> в F001–006 и FA01–06. Подготовка почвы может входить в посадку; отдельное
> обязательное ожидание не нужно. Последнее поручение — только локальные документы,
> без нового изменения кода/сервера/GitHub/исходного архива.
>
> **D-104:** реализация начата; текущие статусы находятся в
> [IMPLEMENTATION_PROGRESS_1_0.json](IMPLEMENTATION_PROGRESS_1_0.json) и машинном backlog.
> Ниже — порядок и критерии, составленные при документальной сверке D-103.

# Порядок задач 1.0 и точки интеграции

20.10.2026. Машинная очередь: [INTEGRATED_BACKLOG_1_0.json](INTEGRATED_BACKLOG_1_0.json).
Оригинальный ZIP получен и проверен: 23 файла, 22 SHA-256 manifest, CRC архива. Все 47 исходных карточек прочитаны из оригинала; добавлены 6 карточек общей фермы. Это полный объём53карточек; актуальные результаты реализации и ограничения учитываются отдельно от исходного анализа.

Имена с `PKG-` относятся к пакету, F001–F006 — к дополнению. Исторические D-102/D-103 репозитория не перенумеровываются. Полные шаги, module entrypoints/test files, решения, scopes и критерии готовности каждой карточки сохранены в JSON. Поле `current_support` у каждой карточки отличает найденную основу, частичную реализацию, дефект и новую работу; это статический аудит, без свежего runtime PASS. К 19 исходным модулям добавлена карта Farming. Общий контракт проверки: успех, отказ, повтор, конкуренция, рестарт, миграция и наблюдаемый результат; фактический PASS только после отдельной реализации.

| ID | Изменение | После | Модули интеграции | Проверки |
| --- | --- | --- | --- | --- |
| PKG-T000 | Сверить свежую базу и инструкции | — | validation, bot_validation | сверка документов |
| PKG-T001 | Разобрать существенные открытые решения | PKG-T000 | решения / активы | сверка документов |
| PKG-T002 | Изолированная тестовая среда | PKG-T000 | validation, bot_validation | PKG-A34 |
| PKG-T003 | Воспроизводимая регрессия и часы | PKG-T002 | validation, bot_validation | PKG-A03, PKG-A34 |
| PKG-T004 | База производительности и целевой масштаб | PKG-T002 | validation, combat, client_sync | PKG-A35 |
| PKG-T010 | Идентичность валют и совместимые labels | PKG-T001, PKG-T003 | economy, ownership_cost, bot_wallet, economy_ui, data | PKG-A01, PKG-A34 |
| PKG-T011 | Проверить учёт и границы денежных команд | PKG-T003, PKG-T010 | economy, data | PKG-A02, PKG-A03, PKG-A08 |
| PKG-T012 | Серверная политика безопасного отдыха | PKG-T001, PKG-T003 | presence_rest, combat, dungeons, data | PKG-A12, PKG-A13, PKG-A34 |
| PKG-T013 | Команды и проекции после reconnect | PKG-T003 | client_sync, dungeons, economy | PKG-A03, PKG-A13, PKG-A17 |
| PKG-T014 | Мобильная оболочка и контекстные действия | PKG-T010, PKG-T012, PKG-T013 | client_sync, economy_ui, feedback_notifications | PKG-A32, PKG-A31 |
| PKG-T015 | Права царя, посадника и доверенных сервисов | PKG-T001, PKG-T003 | work_building, director, economy | PKG-A14, PKG-A30 |
| PKG-T020 | Себестоимость материалов и производства | PKG-T011 | ownership_cost, economy, data | PKG-A04, PKG-A05 |
| PKG-T021 | Явная собственность вида работы | PKG-T020, PKG-T015 | ownership_cost, work_building, economy_ui | PKG-A07, PKG-A04 |
| PKG-T022 | Ограниченные госзаказы и резерв зарплаты | PKG-T011, PKG-T021 | economy, work_building, data, economy_ui | PKG-A06, PKG-A07 |
| PKG-T023 | Рынок, заказы поставки и безопасный резерв | PKG-T020, PKG-T021 | economy, economy_ui | PKG-A08, PKG-A04, PKG-A07 |
| PKG-T024 | Версионированная дань на согласованную базу | PKG-T020, PKG-T023 | economy, data, economy_ui | PKG-A09, PKG-A05, PKG-A10 |
| PKG-T025 | Оборот казны, эмиссия и настоящие изъятия | PKG-T022, PKG-T024 | economy, data | PKG-A10, PKG-A11 |
| PKG-T026 | Экономическая симуляция и первый хозяйственный срез | PKG-T022, PKG-T023, PKG-T024, PKG-T025, F005, F006 | economy, ownership_cost, economy_ui | PKG-A11, PKG-A37 |
| PKG-T030 | Контракт группы и распределения похода | PKG-T012, PKG-T015 | dungeons, data, client_sync | PKG-A15 |
| PKG-T031 | Ручной одиночный микробой | PKG-T012, PKG-T013 | combat, world, client_sync | PKG-A13, PKG-A14, PKG-A17 |
| PKG-T032 | Связанный групповой данж | PKG-T030, PKG-T013 | dungeons, client_sync, economy | PKG-A15, PKG-A03 |
| PKG-T033 | Осмысленное расширение округи | PKG-T004, PKG-T015 | world, combat, client_sync | PKG-A17, PKG-A35, PKG-A34 |
| PKG-T034 | Подготовка приключения через хозяйство | PKG-T026, PKG-T032, F006 | economy, dungeons, work_building | PKG-A03, PKG-A15, PKG-A38 |
| PKG-T035 | Плавность боя и сквозной мобильный опыт | PKG-T014, PKG-T031, PKG-T032, PKG-T033, PKG-T034, PKG-T004 | client_sync, combat, dungeons | PKG-A17, PKG-A32, PKG-A35 |
| PKG-T040 | Массовая NPC-цель и царский поход | PKG-T030, PKG-T033, PKG-T034, PKG-T035 | combat, world, dungeons, economy | PKG-A14, PKG-A16, PKG-A35 |
| PKG-T041 | Последняя осада как сохранённая фаза | PKG-T040, PKG-T012 | siege_recovery, combat, data | PKG-A40, PKG-A14 |
| PKG-T042 | Редкая помощь с ценой и квитанцией | PKG-T041 | siege_recovery, combat, data | PKG-A19, PKG-A40 |
| PKG-T043 | Восстановление и социальное лечение | PKG-T041, PKG-T022, PKG-T025 | siege_recovery, work_building, economy | PKG-A18, PKG-A06, PKG-A11 |
| PKG-T050 | Снимок мира и каталог полномочий | PKG-T015, PKG-T025, PKG-T030 | director, economy, combat, dungeons | PKG-A20, PKG-A22 |
| PKG-T051 | Идемпотентный исполнитель решений | PKG-T050, PKG-T011 | director, economy, data | PKG-A20, PKG-A22 |
| PKG-T052 | Сценарный ритм и отказоустойчивое расписание | PKG-T050, PKG-T041, PKG-T042, PKG-T043 | director, combat, siege_recovery | PKG-A21, PKG-A18, PKG-A19 |
| PKG-T053 | Gemini через существующую модельную цепочку | PKG-T050 | bot_ai, director | PKG-A21, PKG-A22 |
| PKG-T054 | Экономические действия всевышнего | PKG-T051, PKG-T024, PKG-T025, PKG-T026 | director, economy | PKG-A23, PKG-A09, PKG-A10 |
| PKG-T055 | Самостоятельность после теневого сравнения | PKG-T051, PKG-T052, PKG-T053, PKG-T054 | director, bot_ai | PKG-A20, PKG-A21, PKG-A23, PKG-A39 |
| PKG-T060 | Основной Dcoin батюшки и историческое лечение | PKG-T010, PKG-T011 | bot_wallet, bot_payments, economy_ui | PKG-A01, PKG-A03, PKG-A29, PKG-A34 |
| PKG-T061 | YouTube entitlement и подтверждённый срок | PKG-T015, PKG-T060 | bot_memberships, bot_payments | PKG-A26, PKG-A30 |
| PKG-T062 | Каталог умеренных premium льгот | PKG-T061, PKG-T026 | bot_memberships, presence_rest, economy, work_building | PKG-A24, PKG-A25, PKG-A29 |
| PKG-T063 | Stars: надёжный заказ и развлечительный Dcoin | PKG-T060, PKG-T011 | bot_payments, bot_wallet | PKG-A27, PKG-A28, PKG-A29 |
| PKG-T064 | Настройки уведомлений и доступ по группе | PKG-T014, PKG-T015 | feedback_notifications, bot_memberships, bot_wallet | PKG-A30, PKG-A31 |
| PKG-T065 | Связанные мини-игры и общий бюджет наград | PKG-T060, PKG-T062 | bot_wallet, bot_payments, economy | PKG-A03, PKG-A24, PKG-A29 |
| PKG-T070 | Музыкальные состояния и события | PKG-T013, PKG-T030, PKG-T041 | client_sync, combat, dungeons | PKG-A33 |
| PKG-T071 | Оригинальный аудио/визуальный набор | PKG-T001 | решения / активы | PKG-A33, PKG-A32 |
| PKG-T072 | Телефонный звук и баланс стрима | PKG-T070, PKG-T071, PKG-T014 | client_sync | PKG-A33, PKG-A32 |
| PKG-T080 | Длительная жизнь и общий игровой результат | PKG-T034, PKG-T040, PKG-T043, PKG-T055, PKG-T064, PKG-T065, PKG-T072, F005, F006 | economy, director, combat, dungeons, bot_wallet | PKG-A11, PKG-A18, PKG-A25, PKG-A36 |
| PKG-T081 | Нагрузка и физические телефоны | PKG-T080, PKG-T004, PKG-T035 | validation, client_sync, combat | PKG-A35, PKG-A32, PKG-A21 |
| PKG-T082 | Репетиция миграции и обратимого включения | PKG-T080, PKG-T060, PKG-T063 | data, economy, bot_wallet, director | PKG-A34, PKG-A01, PKG-A28 |
| PKG-T083 | Финальная готовность к будущему выпуску | PKG-T081, PKG-T082 | validation, bot_validation | PKG-A36, PKG-A34 |
| F001 | Контракт общей фермы и версии правил | PKG-T001, PKG-T012, PKG-T020, PKG-T021 | farming, work_building, ownership_cost, economy, data, economy_ui, presence_rest | FA01, FA05, FA06 |
| F002 | Адресная грядка, назначение и совместимость | F001, PKG-T011, PKG-T015 | farming, work_building, ownership_cost, economy, data, economy_ui, presence_rest | FA02, FA03 |
| F003 | Ручной уход и полезный вклад | F002 | farming, work_building, ownership_cost, economy, data, economy_ui, presence_rest | FA02, FA03, FA05 |
| F004 | Урожай, натуральная доля и один фонд труда | F003, PKG-T022, PKG-T025 | farming, work_building, ownership_cost, economy, data, economy_ui, presence_rest | FA01, FA02, FA04, FA05 |
| F005 | Фермерское окно в текущем интерфейсе | F004, PKG-T014 | farming, work_building, ownership_cost, economy, data, economy_ui, presence_rest | FA03, FA04 |
| F006 | Баланс еды/труда/рынка и восстановление фермы | F004, PKG-T023, PKG-T024, PKG-T025 | farming, work_building, ownership_cost, economy, data, economy_ui, presence_rest | FA01, FA05, FA06 |

## Условия перехода

M0 фиксирует базу и будущую изолированную среду; M1 — валютные идентификаторы, транзакции, отдых, reconnect и права. M2 исправляет CostBasis, определяет результат public/private, резервирует госзаказ, рынок и дань. Ферма F001–F006 выполняется здесь: T026 зависит от F005/F006. F006 не зависит от T026/T034/T043, поэтому питание и восстановление не образуют круг.

T034 использует подтверждённый хозяйственный срез и групповой данж. T035 включает проверку всех боевых потоков, включая одиночную сцену и округу. Затем массовая NPC-цель, последняя осада, редкое чудо и восстановление. Сценарный директор ждёт эти исходы; экономический исполнитель ждёт реальный налог/казну/T026. Автономия не заканчивается на Propose.

YouTube/Stars/мини-игры, доступ/уведомления и музыка могут готовиться по собственным зависимостям. Итоговый T080 включает хозяйство, ферму, поход, поражение/восстановление, автономию, уведомления, развлекательный контур и звук; T081/T082 измеряют телефоны/нагрузку и репетируют миграцию; T083 собирает готовность к будущему выпуску.

Проверенный граф: 53 карточки, 23 слоёв, нет циклов/неизвестных рёбер; все входят в транзитивные зависимости PKG-T083. Это проверка плана, не результат игровых тестов.

Исходные A01–A40 и новые FA01–FA06 полностью описаны в JSON; все результаты planned, не PASS. acceptance_scope отличает раннюю компонентную проверку от полного кейса. Например, T026/A37 не ждёт будущей осады/premium, T034/A38 — всего релиза, T055/A39 — платежей/музыки, T041/A40 — ещё не созданного восстановления. Ни одно OPEN решение не выдаётся за закрытое; оно ограничивает соответствующую карточку, а не независимое чтение/документацию.
